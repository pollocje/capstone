using System.Collections.Generic;
using UnityEngine;

namespace LuigiGameDev.CarController.Sample
{
    public class BallHitSound : MonoBehaviour
    {
        [SerializeField]
        private AudioSource m_HitAudioSource;

        [SerializeField]
        private AudioSource m_HitMetalAudioSource;

        [SerializeField]
        private List<GameObject> m_MetalObjects = new();

        [SerializeField]
        [Min(0.0f)]
        private float m_MaxHitVelocity = 30.0f;

        private void OnCollisionEnter(Collision collision)
        {
            float velocityNormalized = collision.relativeVelocity.magnitude / m_MaxHitVelocity;

            if (m_MetalObjects.Contains(collision.gameObject))
            {
                m_HitMetalAudioSource.volume = velocityNormalized;
                m_HitMetalAudioSource.Play();
            }
            else
            {
                m_HitAudioSource.volume = velocityNormalized;
                m_HitAudioSource.Play();
            }
        }
    }
}
