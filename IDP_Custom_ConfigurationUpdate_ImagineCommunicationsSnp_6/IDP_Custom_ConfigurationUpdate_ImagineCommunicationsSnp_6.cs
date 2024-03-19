/*
****************************************************************************
*  Copyright (c) 2022,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this driver, you expressly agree with the usage terms and
conditions set out below.
This driver and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this driver is strictly for personal use only.
This driver may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
driver is forbidden.

Any modifications to this driver by the user are only allowed for
personal use and within the intended purpose of the driver,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the driver resulting from a modification
or adaptation by the user.

The content of this driver is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENT
06/04/2022	1.0.0.1		ADK, Skyline	Initial version.
13/03/2024  1.0.0.2     DBO, Skyline    [DCP 180437] Fixed update functionality.

*/

using System;
using System.IO;
using System.Linq;

using Core.Defaults;
using Core.Generic;

using IDP.Common;

using Newtonsoft.Json;

using Skyline.DataMiner.Automation;
using Skyline.DataMiner.DataMinerSolutions.IDP.ConfigurationManagement;

public class Script
{
	private Update configurationUpdate;
	private IEngine engine;

	public void Run(IEngine engine)
	{
		try
		{
			this.engine = engine;

			// This method will communicate with the IDP solution, to provide the required feedback for the update process.
			configurationUpdate = new Update(engine);

			// When the configuration update process starts normally, this method should be called.
			configurationUpdate.NotifyProcessStarted();

			string filePath = configurationUpdate.InputData.FileLocation;

			string data = GetDataFromFile(filePath);

			// In here, the data will be sent to the element. The code within it should be customized.
			if (!SetDataOnElement(engine, data))
			{
				// On unexpected cases that are not the result of exceptions, the script should still inform IDP of the failure.
				configurationUpdate.NotifyProcessFailure("There was an issue while setting data on the element.");
				return;
			}

			// On a normal execution, IDP should be notified of its success through this method.
			configurationUpdate.NotifyProcessSuccess();
		}
		catch (ScriptAbortException)
		{
			// For any exceptions, or other exceptional/unexpected behavior, the IDP solution should be informed, if possible, of the failure.
			configurationUpdate?.NotifyProcessFailure("Update script was aborted.");
			throw;
		}
		catch (FileLoadException ex)
		{
			configurationUpdate?.NotifyProcessFailure($"There was an issue while loading the data from the file:{Environment.NewLine}{ex}");
			engine.ExitFail(ex.ToString());
		}
		catch (Exception ex)
		{
			configurationUpdate?.NotifyProcessFailure($"The main script failed due to an exception:{Environment.NewLine}{ex}");
			engine.ExitFail(ex.ToString());
		}
	}

	private static string GetDataFromFile(string filePath)
	{
		if (String.IsNullOrWhiteSpace(filePath))
		{
			throw new ArgumentException("The provided path is invalid.", nameof(filePath));
		}

		string data = File.ReadAllText(filePath);

		if (String.IsNullOrWhiteSpace(data))
		{
			throw new FileLoadException("File could not be read properly.");
		}

		return data;
	}

	private bool SetDataOnElement(IEngine engine, string data)
	{
		try
		{
			var backupData = JsonConvert.DeserializeObject<BackupDataSourceIp>(data);

			Element element = engine.FindElement(configurationUpdate.InputData.Element.AgentId, configurationUpdate.InputData.Element.ElementId);

			VerifyPresetAvailability(element, backupData.FileName);

			element.SetParameter(3706 /* Load button for one row in preset table */, backupData.FileName, "1");
			engine.GenerateInformation("Loading the new preset can take up to 8 minutes. Please be patient...");
			engine.Sleep(480_000);

			var isActive = IsElementActive(element);

			return isActive;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private void ExportPresetToDevice(Element element, string fileName)
	{
		engine.GenerateInformation("Exporting backup preset to device...");

		string defaultBackupPresetFolderPath = GlobalDefaults.DefaultBackupPresetFolderPath;
		string storedPresetFolderPath = Convert.ToString(element.GetParameter(3685 /* Preset folder path */));

		element.SetParameter(3685 /* Preset folder path */, defaultBackupPresetFolderPath);
		element.SetParameter(3681 /* Get DMA presets button */, "1");
		engine.Sleep(5_000);
		element.SetParameter(3683 /* Preset filename */, fileName + ".prst");

		engine.Sleep(5_000);

		element.SetParameter(3685 /* Preset folder path */, storedPresetFolderPath);
	}

	private void VerifyPresetAvailability(Element element, string backupPresetFileName)
	{
		const int presetTableCheckTimeoutInMinutes = 2;
		const int presetTableCheckRetryInterval = 5_000;

		bool isPresetAvailable = GenericHelper.Retry(
			() =>
			{
				string[] primaryKeys = element.GetTablePrimaryKeys(3700 /* Presets table */);

				if (primaryKeys.Contains(backupPresetFileName))
				{
					engine.GenerateInformation("The backup preset exists in table.");
					return true;
				}

				ExportPresetToDevice(element, backupPresetFileName);

				element.SetParameter(50012 /* Poll Manager Actions - Refresh button */, "Preset", "1");
				return false;
			},
			TimeSpan.FromMinutes(presetTableCheckTimeoutInMinutes),
			presetTableCheckRetryInterval);

		if (!isPresetAvailable)
		{
			engine.GenerateInformation("ERR: Preset is not available in preset table... Please be aware that this device has a limitation where only 20 presets can be stored.");
			throw new UpdateFailedException("Preset is not available in preset table.");
		}
	}

	private bool IsElementActive(IActionableElement element)
	{
		const int restartingTimeoutInMinutes = 2;
		const int restartingRetryInterval = 10_000;

		bool isActive = GenericHelper.Retry(
			() =>
			{
				engine.GenerateInformation("Restarting the element...");

				var timeoutThreshold = element.ElementInfo.MainPort.TimeoutTime;
				element.Restart();

				engine.Sleep(timeoutThreshold);

				Element[] elements = engine.FindElements(new ElementFilter { DataMinerID = element.DmaId, ElementID = element.ElementId, TimeoutOnly = true });

				if (elements.Length == 1)
				{
					engine.GenerateInformation("Element in timeout...");
					return false;
				}

				return true;
			},
			TimeSpan.FromMinutes(restartingTimeoutInMinutes),
			restartingRetryInterval);

		if (!isActive)
		{
			engine.GenerateInformation("ERR: Element in timeout... Please check.");
			throw new UpdateFailedException("Element remains in timeout.");
		}
		else
		{
			engine.GenerateInformation("The backup preset is loaded and element is active again.");
			return true;
		}
	}
}