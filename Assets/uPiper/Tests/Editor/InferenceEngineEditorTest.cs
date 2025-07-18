using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Unity AI Interface (Inference Engine) のエディタ専用テスト
    /// </summary>
    public class InferenceEngineEditorTest
    {
        [Test]
        public void CheckInferenceEnginePackageVersion()
        {
            // パッケージ情報を確認
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(Unity.InferenceEngine.Model).Assembly
            );

            if (packageInfo != null)
            {
                Debug.Log($"Inference Engine package: {packageInfo.name}");
                Debug.Log($"Version: {packageInfo.version}");
                Debug.Log($"Display Name: {packageInfo.displayName}");

                Assert.IsNotNull(packageInfo.version, "Package version should be available");
                Assert.AreEqual("com.unity.ai.inference", packageInfo.name, "Package name should be com.unity.ai.inference");
            }
            else
            {
                Debug.LogWarning("Package info not found - package might be embedded or built-in");
            }
        }

        [Test]
        public void CheckBurstPackageVersion()
        {
            // Burst パッケージの確認
            var burstAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Contains("Unity.Burst"));

            if (burstAssembly != null)
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(burstAssembly);
                if (packageInfo != null)
                {
                    Debug.Log($"Burst package version: {packageInfo.version}");
                    Assert.IsNotNull(packageInfo.version, "Burst package should be installed");
                }
            }
        }
    }
}