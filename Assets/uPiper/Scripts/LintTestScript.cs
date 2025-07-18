using UnityEngine;
using System;

namespace uPiper.Scripts
{
    public class LintTestScript : MonoBehaviour
    {
        // フィールドのテスト
        // ❌ 警告: privateフィールドは_で始まるべき
        private IntPtr openjtalkHandle = IntPtr.Zero;
        
        // ✅ OK: _で始まっている
        private IntPtr _openjtalkHandle = IntPtr.Zero;
        
        // ❌ 警告: private staticフィールドはs_で始まるべき
        private static int instanceCount = 0;
        
        // ✅ OK: s_で始まっている
        private static int s_instanceCount = 0;
        
        // ✅ OK: publicフィールドはPascalCase
        public string PublicField = "test";
        
        // ✅ OK: constはPascalCase
        private const string ConstantValue = "constant";

        // メソッドのテスト
        // ❌ 警告: アクセス修飾子がない
        private void Start()
        {
            Debug.Log("Start without access modifier");
        }

        // ✅ OK: private明示
        private void Update()
        {
            Debug.Log("Update with private");
        }

        // ✅ OK: protected（UNT0021推奨）
        protected void OnEnable()
        {
            Debug.Log("OnEnable with protected");
        }

        // ❌ 警告: アクセス修飾子がない
        private void Awake()
        {
            Debug.Log("Awake without access modifier");
        }

        // ❌ 警告: 通常のメソッドもアクセス修飾子が必要
        private void CustomMethod()
        {
            Debug.Log("Custom method without access modifier");
        }
        
        // ✅ OK: private明示
        private void ValidMethod()
        {
            // ローカル変数はcamelCase（_不要）
            int localVariable = 10;
            string anotherLocal = "test";
        }
    }
}