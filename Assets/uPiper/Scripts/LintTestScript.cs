using UnityEngine;

namespace uPiper.Scripts
{
    public class LintTestScript : MonoBehaviour
    {
        // これは警告を出すべき（アクセス修飾子がない）
        void Start()
        {
            Debug.Log("Start without access modifier");
        }

        // これはOK（private明示）
        private void Update()
        {
            Debug.Log("Update with private");
        }

        // これもOK（protected）
        protected void OnEnable()
        {
            Debug.Log("OnEnable with protected");
        }

        // これも警告を出すべき（アクセス修飾子がない）
        void Awake()
        {
            Debug.Log("Awake without access modifier");
        }

        // 通常のメソッド（警告を出すべき）
        void CustomMethod()
        {
            Debug.Log("Custom method without access modifier");
        }
    }
}