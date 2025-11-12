using UnityEngine;

public class abTest : MonoBehaviour
{

    public Transform posAnchor;
    public Transform rotAnchor;

    public ArticulationBody ab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ab = GetComponent<ArticulationBody>();
    }

    // Update is called once per frame
    void Update()
    {
        posAnchor.position = transform.position + ab.anchorPosition;
        rotAnchor.position = transform.position + ab.anchorPosition + ab.anchorRotation * transform.rotation * Vector3.up * 0.05f;

    }
}
