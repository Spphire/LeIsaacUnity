using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using System.Collections.Generic;

public class ArticulationIK : MonoBehaviour
{
    [Header("末端执行器")]
    public ArticulationBody endEffector;

    [Header("目标 Transform")]
    public Transform target;

    [Header("阻尼参数")]
    public float damping = 0.1f;
    
    [Header("权重参数")]
    [Range(0f, 1f)]
    public float wp = 1.0f;  // 位置权重
    [Range(0f, 1f)]
    public float wr = 0.2f;  // 旋转权重（降低旋转的影响）
    
    private List<ArticulationBody> jointChain = new List<ArticulationBody>();
    private int dof;

    void Start()
    {
        BuildJointChain();
    }

    void FixedUpdate()
    {
        if (endEffector == null || target == null) return;

        SolveIK();
    }

    void BuildJointChain()
    {
        jointChain.Clear();
        ArticulationBody current = endEffector;
        while (current != null)
        {
            if (current.jointType == ArticulationJointType.RevoluteJoint)
            {
                jointChain.Insert(0, current);
            }
            current = current.transform.parent != null ? current.transform.parent.GetComponent<ArticulationBody>() : null;
        }
        dof = jointChain.Count-1;
    }

    void SolveIK()
    {
        // 1. 末端当前 Transform
        Vector3 currentPos = endEffector.transform.position;
        Quaternion currentRot = endEffector.transform.rotation;

        // 2. 位置误差
        Vector3 posError = target.position - currentPos;

        // 3. 旋转误差 (用轴角表示)
        Quaternion deltaRot = target.rotation * Quaternion.Inverse(currentRot);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 rotError = axis.normalized * Mathf.Deg2Rad * angleDeg;

        // 4. 误差向量 6x1
        var e = Vector<float>.Build.Dense(6);
        e[0] = posError.x * wp; e[1] = posError.y * wp; e[2] = posError.z * wp;
        e[3] = rotError.x * wr; e[4] = rotError.y * wr; e[5] = rotError.z * wr;

        // 5. 构建雅可比 6 x dof
        var J = Matrix<float>.Build.Dense(6, dof);
        ArticulationJacobian denseJac = new ArticulationJacobian();
        endEffector.GetDenseJacobian(ref denseJac); // 假设返回 6 x dof
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < dof; j++)
                J[i, j] = denseJac[denseJac.rows-6+i, j];

        // 6. 阻尼伪逆：dq = J^T * (J*J^T + λ^2 I)^-1 * e
        var I6 = Matrix<float>.Build.DenseIdentity(6);
        var JJt = J * J.Transpose();
        var inv = (JJt + damping * damping * I6).Inverse();
        var dq = J.Transpose() * inv * e;

        // 7. 更新关节角度
        List<float> newPositions = new List<float>();
        for (int i = 0; i < dof; i++)
        {
            newPositions.Add(jointChain[i].jointPosition[0] + dq[i]);
        }

        newPositions.Add(0);
        jointChain[0].SetDriveTargets(newPositions);
    }
}
