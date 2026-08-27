using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

namespace EndangeredAR.Tests.EditMode
{
    public sealed class IosReleaseNetworkIsolationTests
    {
        [Test]
        public void ProductionPlist_HasNoAiLocalNetworkOrArbitraryHttpPermission()
        {
            var plist = NewPlist();

            ConfigureNetworkPermissions(plist.root, false);

            Assert.That(plist.root.values.ContainsKey("NSLocalNetworkUsageDescription"), Is.False);
            Assert.That(plist.root.values.ContainsKey("NSAppTransportSecurity"), Is.False);
        }

        [Test]
        public void ExplicitDevelopmentRemoteBuild_AddsOnlyDevelopmentNetworkPermissions()
        {
            var plist = NewPlist();

            ConfigureNetworkPermissions(plist.root, true);

            Assert.That(plist.root.values.ContainsKey("NSLocalNetworkUsageDescription"), Is.True);
            Assert.That(plist.root["NSAppTransportSecurity"].AsDict()["NSAllowsArbitraryLoads"].AsBoolean(), Is.True);
        }

        [Test]
        public void ProductionIosBuilder_IsLockedToNativeRuntimeDeploymentTarget()
        {
            var builderVersion = Constant("EndangeredARIosBuilder", "MinimumIosVersion");
            var postprocessorVersion = Constant(
                "EndangeredAR.Build.OnDeviceLLMIosPostprocessor",
                "MinimumIosVersion");
            Assert.That(builderVersion, Is.EqualTo("16.4"));
            Assert.That(postprocessorVersion, Is.EqualTo(builderVersion));
        }

        private static PlistDocument NewPlist()
        {
            var document = new PlistDocument();
            document.Create();
            return document;
        }

        private static void ConfigureNetworkPermissions(PlistElementDict root, bool enabled)
        {
            var method = FindType("EndangeredARIosPostprocessor").GetMethod(
                "ConfigureDevelopmentNetworkPermissions",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { root, enabled });
        }

        private static string Constant(string typeName, string fieldName)
        {
            var field = FindType(typeName).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null);
            return (string)field.GetRawConstantValue();
        }

        private static Type FindType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Expected {fullName} to exist.");
            return type;
        }
    }
}
