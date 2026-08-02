using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TossParam
{
    public Vector3 tyakuti;
    public float height;
}

public class BallController : MonoBehaviour
{
    Vector3 setter;
    Rigidbody rb;

    public List<TossParam> leftToss = new List<TossParam>();
    public List<TossParam> rightToss = new List<TossParam>();

    int powerLevel = 0;
    int side = 0; // 0=L 1=R

    public float duration = 1.0f;

    float a, b, c;
    bool moving = false;
    float timer = 0f;

    Vector3 currentTyakuti;
    bool courtChanged = false;

    void Start()
    {
        setter = transform.position;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public void SetCourtChanged(bool value)
    {
        courtChanged = value;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            transform.position = setter;
            moving = false;
            timer = 0f;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) powerLevel = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) powerLevel = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) powerLevel = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) powerLevel = 3;
        if (Input.GetKeyDown(KeyCode.Alpha5)) powerLevel = 4;

        if (Input.GetKeyDown(KeyCode.L)) side = 0;
        if (Input.GetKeyDown(KeyCode.R)) side = 1;

        if (Input.GetKeyDown(KeyCode.P))
        {
            TossParam param;

            if (side == 0)
                param = leftToss[powerLevel];
            else
                param = rightToss[powerLevel];

            Vector3 tyakuti = param.tyakuti;

            if (courtChanged)
            {
                // xz平面で原点対称
                tyakuti = new Vector3(-tyakuti.x, tyakuti.y, -tyakuti.z);
            }

            CalculateQuadratic(tyakuti, param.height);

            transform.position = setter;
            timer = 0f;
            moving = true;
        }

        if (moving)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            if (t >= 1f)
            {
                t = 1f;
                moving = false;
            }

            float z = Mathf.Lerp(setter.z, currentTyakuti.z, t);
            float y = a * z * z + b * z + c;

            // xも固定値としてsetter.xを使う
            transform.position = new Vector3(setter.x, y, z);
        }
    }

    void CalculateQuadratic(Vector3 tyakuti, float height)
    {
        currentTyakuti = tyakuti;

        float z0 = setter.z;
        float y0 = setter.y;

        float z2 = tyakuti.z;
        float y2 = tyakuti.y;

        float z1 = (z0 + z2) * 0.5f;
        float y1 = height;

        float denom = (z0 - z1) * (z0 - z2) * (z1 - z2);

        if (Mathf.Abs(denom) < 0.000001f)
        {
            Debug.LogError("zが重複している");
            return;
        }

        a = (z2 * (y1 - y0) + z1 * (y0 - y2) + z0 * (y2 - y1)) / denom;
        b = (z2 * z2 * (y0 - y1) + z1 * z1 * (y2 - y0) + z0 * z0 * (y1 - y2)) / denom;
        c = (z1 * z2 * (z1 - z2) * y0 + z2 * z0 * (z2 - z0) * y1 + z0 * z1 * (z0 - z1) * y2) / denom;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "DeleteZone" || other.gameObject.name == "DeleteZone(1)")
        {
            Destroy(gameObject);
        }
    }
}