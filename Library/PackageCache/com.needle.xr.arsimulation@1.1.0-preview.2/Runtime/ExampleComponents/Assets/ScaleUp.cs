using UnityEngine;

namespace Needle.XR.ARSimulation.ExampleComponents
{
    public class ScaleUp : MonoBehaviour
    {
        private Vector3 targetScale;

        private void Start()
        {
            targetScale = transform.localScale;
            transform.localScale = Vector3.one * 0.00001f;
        }

        private void Update()
        {
            transform.localScale = Vector3.Slerp(transform.localScale, targetScale, Time.deltaTime / .1f);
        }
    }
}

