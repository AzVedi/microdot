﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.Ninject;
using Gigya.Microdot.SharedLogic;
using Gigya.Microdot.UnitTests.Caching.Host;
using Ninject;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.Configuration.Verificator
{
	/// <summary>
	/// The tests to ensure Configuration Verificator is 
	/// </summary>
	public class VerificatorTests
	{
		private readonly string _loadPaths = @"[{ ""Pattern"": "".\\*.config"", ""Priority"": 1 }]";

		[OneTimeSetUp]
		public void Load()
		{
			string APPNAME = "InfraTests";
			CurrentApplicationInfo.Init(APPNAME);
		}

		public (StandardKernel k, IAssemblyProvider providerMock, IFileSystem fileSystemMock) Setup()
		{
			var k = new StandardKernel();
			k.Load(new ConfigVerificationModule(new FakesLoggersModules(false), new ServiceArguments()));

			IAssemblyProvider providerMock = Substitute.For<IAssemblyProvider>();
			providerMock.GetAssemblies().Returns(info => new[] {GetType().Assembly} );

			IFileSystem fileSystemMock = Substitute.For<IFileSystem>();
			fileSystemMock.ReadAllTextFromFile(Arg.Any<string>()).Returns(a => _loadPaths);
			fileSystemMock.Exists(Arg.Any<string>()).Returns(a => true);
			
			k.Rebind<IAssemblyProvider>().ToConstant(providerMock);
			k.Rebind<IFileSystem>().ToConstant(fileSystemMock);

			return (k, providerMock, fileSystemMock);
		}

		[Test]
		public void WhenConfigIsNotValidXml()
		{
			var setup = Setup();

			setup.providerMock.GetAllTypes().Returns(info => new[]
			{
				typeof(VerifiedConfig2),
			});

			setup.fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new []
			{
				"VerifiedConfig2.config",
			});

			setup.fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
			{
				string content;
				if (callinfo.ArgAt<string>(0) == "VerifiedConfig2.config")
				{
					// required attribute is NOT satisfied !!!
					content = @"<configuration with WRONG XML configuration>";
				}
				else
					throw new ArgumentException("Invalid config file name...");

				return Task.FromResult(content);
			});

			var v = setup.k.Get<ConfigurationVerificator>();

			var s = v.Verify();
			
			s.IsSuccess.ShouldBeFalse();

			// --->>>> CONFIGURATION OBJECTS FAILED TO PASS THE VERIFICATION <<<<-----
			// 	TYPE: Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig1
			// PATH :  Missing or invalid configuration file: VerifiedConfig1.config
			// ERROR:  Root element is missing.
			// 	TYPE: Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig2
			// PATH :  Missing or invalid configuration file: VerifiedConfig1.config
			// ERROR:  Root element is missing.

			s.Failed.Any(failure => 
				failure.Type.Name == typeof(VerifiedConfig2).Name &&
				failure.Path.Contains("Missing or invalid configuration file")).ShouldBeTrue();
		}

		[Test]
		public void WhenAnnotationViolated()
		{
			var setup = Setup();

			setup.providerMock.GetAllTypes().Returns(info => new[]
			{
 				typeof(VerifiedConfig2),
 			});

			setup.fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new[]
			{
 				"VerifiedConfig2.config",
 			});

			setup.fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
			{
				string content;
				if (callinfo.ArgAt<string>(0) == "VerifiedConfig2.config")
				{
					 // required attribute is NOT satisfied !!!
					 content = @"<configuration></configuration>";
				}
				else
					throw new ArgumentException("Invalid config file name...");
				
				return Task.FromResult(content);
			});

			var v = setup.k.Get<ConfigurationVerificator>();

			var s = v.Verify();

			// --->>>> CONFIGURATION OBJECTS FAILED TO PASS THE VERIFICATION <<<<-----
			// 	TYPE: Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig2
			// PATH :  VerifiedConfig2
			// ERROR:  The Required field is required.
			// 	The following 1 configuration objects passed the verification:
			// Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig1

			s.IsSuccess.ShouldBeFalse();

			s.Failed.Any(failure =>
				failure.Type.Name == typeof(VerifiedConfig2).Name &&
				failure.Details.Contains("The Required field is required"))
			.ShouldBeTrue("Expected a failure! When no value is given for the property");
		}

		[Test]
		public void WhenValueLoadedFromConfigFile()
		{
			var setup = Setup();

			setup.providerMock.GetAllTypes().Returns(info => new[]
			{
		 				typeof(VerifiedConfig1),
		 			});

			setup.fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new[]
			{
		 				"VerifiedConfig1.config",
		 			});

			setup.fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
			{
				var content = "";
				if (callinfo.ArgAt<string>(0) == "VerifiedConfig1.config")
				{
					content =
@"<configuration>
		 <VerifiedConfig1>
		 	<ValueLoaded>theValue</ValueLoaded>
		 </VerifiedConfig1>
		 </configuration>";
				}
				return Task.FromResult(content);
			});


			var v = setup.k.Get<ConfigurationVerificator>();

			var s = v.Verify();

			setup.k.Get<VerifiedConfig1>().ValueLoaded.ShouldBe("theValue");

			s.Passed.Any(passed => passed == typeof(VerifiedConfig1)).ShouldBeTrue();
			s.IsSuccess.ShouldBeTrue();
		}


		[Test]
		public void WhenValueIsNotSuitable()
		{
			var setup = Setup();

			setup.providerMock.GetAllTypes().Returns(info => new[]
			{
				typeof(VerifiedConfig3),
			});

			setup.fileSystemMock.GetFilesInFolder(Arg.Any<string>(), Arg.Any<string>()).Returns(info => new[]
			{
				"VerifiedConfig3.config",
			});

			setup.fileSystemMock.ReadAllTextFromFileAsync(Arg.Any<string>()).Returns(callinfo =>
			{
				string content;
				if (callinfo.ArgAt<string>(0) == "VerifiedConfig3.config")
				{
					content =
						@"<configuration>
		 <VerifiedConfig3>
		 	<TheInt>theValue</TheInt>
		 </VerifiedConfig3>
		 </configuration>";
				}
				else
					throw new ArgumentException("Invalid config file name...");

				return Task.FromResult(content);
			});


			var v = setup.k.Get<ConfigurationVerificator>();

			var s = v.Verify();

			// --->>>> CONFIGURATION OBJECTS FAILED TO PASS THE VERIFICATION <<<<-----
			// 	TYPE: Gigya.Microdot.UnitTests.Configuration.Verificator.VerifiedConfig3
			// PATH :  VerifiedConfig3
			// ERROR:  Failed to deserialize config object: Could not convert string to integer: theValue. Path 'TheInt'.


			s.IsSuccess.ShouldBeFalse();

			s.Failed.Any(failure =>
					failure.Type.Name == typeof(VerifiedConfig3).Name &&
					failure.Details.Contains("Failed to deserialize config object"))
				.ShouldBeTrue("Test is expected to fail while string cannot be converted to an int.");
		}
	}
}
