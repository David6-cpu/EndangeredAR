using UnityEngine;

namespace EndangeredAR.Animals
{
    public class AnimalModelController : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        public void PlayIdle()
        {
            animator?.SetTrigger("Idle");
        }

        public void PlayHappy()
        {
            animator?.SetTrigger("Happy");
        }

        public void SetScale(float scale)
        {
            transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.3f, 2.5f);
        }
    }
}

