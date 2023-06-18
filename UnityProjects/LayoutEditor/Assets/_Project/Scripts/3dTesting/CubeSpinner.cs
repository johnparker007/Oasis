using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeSpinner : MonoBehaviour
{
    public float xRotationSpeed = 0.3f;
    public float yRotationSpeed = 0.3f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 euler = transform.eulerAngles;

        euler.x += Time.deltaTime * xRotationSpeed;
        euler.y += Time.deltaTime * xRotationSpeed;

        transform.eulerAngles = euler;
    }
}
