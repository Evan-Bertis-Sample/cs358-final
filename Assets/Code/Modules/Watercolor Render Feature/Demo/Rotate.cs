using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LostInLeaves.WatercolorRendering.Demo
{
    public class Rotate : MonoBehaviour
    {
        public enum RotationMode
        {
            Linear, Oscillate
        }

        [SerializeField] private RotationMode _mode = RotationMode.Oscillate;
        [SerializeField] private float _speed = 10.0f;
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        [SerializeField] private float _oscillationRange = 45.0f; // in degrees
        private Quaternion _initialRotation;

        private void Start()
        {
            _initialRotation = transform.rotation;
        }

        private void Update()
        {
            switch (_mode)
            {
                case RotationMode.Linear:
                    transform.Rotate(_rotationAxis, _speed * Time.deltaTime);
                    break;
                case RotationMode.Oscillate:
                    Quaternion rotation = Quaternion.Euler(_rotationAxis * Mathf.Sin(Time.time * _speed) * _oscillationRange);
                    transform.rotation = _initialRotation * rotation;
                    break;
            }
        }
    }
}