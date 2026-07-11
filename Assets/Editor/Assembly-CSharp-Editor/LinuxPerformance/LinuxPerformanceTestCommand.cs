////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the U3 SDK: https://github.com/smartlydressedgames/u3-sdk/    //
// Please refer to the included LICENSE.txt for copyright notice and license details. //
////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SDG.Unturned.LinuxPerformance.Editor
{
	public static class LinuxPerformanceTestCommand
	{
		public static void RunEditMode()
		{
			Run(TestMode.EditMode);
		}

		public static void RunPlayMode()
		{
			Run(TestMode.PlayMode);
		}

		private static void Run(TestMode mode)
		{
			string resultPath = GetCommandLineValue("-testResults");
			if (string.IsNullOrEmpty(resultPath))
				resultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", mode.ToString().ToLowerInvariant() + "-linux-performance.xml"));
			Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
			if (File.Exists(resultPath))
				File.Delete(resultPath);

			TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
			api.RegisterCallbacks(new Callback(resultPath));
			ExecutionSettings settings = new ExecutionSettings(new Filter()
			{
				testMode = mode,
			});
			api.Execute(settings);
		}

		private static string GetCommandLineValue(string key)
		{
			string[] args = Environment.GetCommandLineArgs();
			for (int i = 0; i + 1 < args.Length; ++i)
			{
				if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
					return args[i + 1];
			}
			return null;
		}

		private sealed class Callback : ICallbacks
		{
			public Callback(string resultPath)
			{
				this.resultPath = resultPath;
			}

			public void RunStarted(ITestAdaptor testsToRun)
			{
			}

			public void RunFinished(ITestResultAdaptor result)
			{
				int total = result.PassCount + result.FailCount + result.SkipCount;
				int failed = result.FailCount;
				int skipped = result.SkipCount;
				int passed = Math.Max(0, total - failed - skipped);
				WriteResult(result, total, passed, failed, skipped);
				if (total <= 0)
					EditorApplication.Exit(4);
				else if (failed > 0)
					EditorApplication.Exit(5);
				else
					EditorApplication.Exit(0);
			}

			public void TestStarted(ITestAdaptor test)
			{
			}

			public void TestFinished(ITestResultAdaptor result)
			{
			}

			private void WriteResult(ITestResultAdaptor result, int total, int passed, int failed, int skipped)
			{
				XmlWriterSettings settings = new XmlWriterSettings()
				{
					Encoding = Encoding.UTF8,
					Indent = true,
				};
				using (XmlWriter writer = XmlWriter.Create(resultPath, settings))
				{
					writer.WriteStartDocument();
					writer.WriteStartElement("test-run");
					writer.WriteAttributeString("total", total.ToString());
					writer.WriteAttributeString("passed", passed.ToString());
					writer.WriteAttributeString("failed", failed.ToString());
					writer.WriteAttributeString("skipped", skipped.ToString());
					writer.WriteAttributeString("result", result.ResultState ?? string.Empty);
					WriteNode(writer, result);
					writer.WriteEndElement();
					writer.WriteEndDocument();
				}
			}

			private static void WriteNode(XmlWriter writer, ITestResultAdaptor result)
			{
				if (result == null)
					return;

				writer.WriteStartElement(result.HasChildren ? "test-suite" : "test-case");
				writer.WriteAttributeString("name", result.Name ?? string.Empty);
				writer.WriteAttributeString("fullName", result.FullName ?? string.Empty);
				writer.WriteAttributeString("result", result.ResultState ?? string.Empty);
				writer.WriteAttributeString("passed", result.PassCount.ToString());
				writer.WriteAttributeString("failed", result.FailCount.ToString());
				writer.WriteAttributeString("skipped", result.SkipCount.ToString());
				if (!string.IsNullOrEmpty(result.Message))
				{
					writer.WriteStartElement("failure");
					writer.WriteElementString("message", result.Message);
					writer.WriteElementString("stack-trace", result.StackTrace ?? string.Empty);
					writer.WriteEndElement();
				}
				if (result.HasChildren)
				{
					foreach (ITestResultAdaptor child in result.Children)
						WriteNode(writer, child);
				}
				writer.WriteEndElement();
			}

			private readonly string resultPath;
		}
	}
}
