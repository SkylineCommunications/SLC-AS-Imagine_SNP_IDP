/*
****************************************************************************
*  Copyright (c) 2022,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
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

DATE		VERSION		AUTHOR			COMMENTS
06/04/2022	1.0.0.1		ADK, Skyline	Initial version

*/

using System;
using System.Runtime.Serialization;

using Skyline.DataMiner.Automation;
using Skyline.DataMiner.DataMinerSolutions.DataMinerSystem;
using Skyline.DataMiner.DataMinerSolutions.IDP.Software;

/// <summary>
/// DataMiner Script Class.
/// </summary>
public class Script
{
	private SoftwareUpdate softwareUpdate;

	/// <summary>
	/// The Script entry point.
	/// </summary>
	/// <param name="engine">Link with SLAutomation process.</param>
	public void Run(IEngine engine)
	{
		engine.Timeout = new TimeSpan(0, 30, 0);
		try
		{
			softwareUpdate = new SoftwareUpdate(engine);
			softwareUpdate.NotifyProcessStarted();

			PerformUpgrade(engine);
		}
		catch (ScriptAbortException)
		{
			softwareUpdate?.NotifyProcessFailure("Script aborted");
			throw;
		}
		catch (Exception e)
		{
			softwareUpdate?.NotifyProcessFailure(e.ToString());
			engine.ExitFail($"Exception thrown{Environment.NewLine}{e}");
		}
	}

	private void PerformUpgrade(IEngine engine)
	{
		InputData inputParameters = softwareUpdate.InputData;
		IElement element = inputParameters.Element;
		string ciType = engine.GetScriptParam("CI Type").Value.ToString();




		engine.GenerateInformation("File location: " + inputParameters.ImageFileLocation);
		IActionableElement dataMinerElement = engine.FindElement(element.AgentId, element.ElementId);
		string sFileName = string.Format("SNP-Firmware-{0}.tgz", GetVersionBaseline(engine, ciType));


		PushUpgradeToDevice(dataMinerElement, inputParameters.ImageFileLocation, sFileName);
		ValidateResult(engine, dataMinerElement);

	}
	private static string GetVersionBaseline(IEngine engine, string primaryKey)
	{
		IActionableElement element = engine.FindElement("DataMiner IDP CI Types");
		return element.GetParameter(404, primaryKey).ToString();
	}

	private void PushUpgradeToDevice(IActionableElement element, string imageFileLocation, string FileName)
	{
		try
		{
			//Changes Stacey 


			element.SetParameter(520, imageFileLocation);

			element.SetParameter(510, FileName);

			element.SetParameter(511, "1"); // Upload

		}
		catch (Exception e)
		{
			softwareUpdate.NotifyProcessFailure(
				$"Failed to issue software update command to element{Environment.NewLine}{e}");
		}
	}

	private void ValidateResult(IEngine engine, IActionableElement dataMinerElement)
	{
		engine.Sleep(300000);
		bool restarting = false;

		for (int i = 0; i < 20; i++)
		{
			engine.Sleep(60000);

			Skyline.DataMiner.Automation.Element[] elements = engine.FindElements(new ElementFilter { DataMinerID = dataMinerElement.DmaId, ElementID = dataMinerElement.ElementId, TimeoutOnly = true });

			if (elements.Length == 1)
			{
				restarting = true;

			}
			else
			{
				restarting = false;
				break;
			}

			//read progress update
		}

		if (restarting)
		{
			engine.GenerateInformation("ERR:Element remains in timeout (> 10 min)");
			throw new UpdateFailedException("Element remains in timeout");
		}

		dataMinerElement.SetParameter(50012, "1", "1"); //Refresh general parameters

		softwareUpdate.NotifyProcessSuccess();
	}
}

[Serializable]
public class UpdateFailedException : Exception
{
	public UpdateFailedException()
	{
	}

	public UpdateFailedException(string message) : base(message)
	{
	}

	public UpdateFailedException(string message, Exception innerException) : base(message, innerException)
	{
	}

	protected UpdateFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
	{
	}
}