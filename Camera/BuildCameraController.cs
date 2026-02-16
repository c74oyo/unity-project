using UnityEngine;

public class BuildCameraController : MonoBehaviour
{
    [Header("Refs")]
    public Transform pitchPivot;     // 固定俯角的节点
    public Camera cam;               // 实际摄像机

    [Header("Move (XZ)")]
    public float moveSpeed = 15f;
    public float sprintMultiplier = 2f;

    [Header("Rotate (Hold Mouse)")]
    public int rotateMouseButton = 1;    // 1 = 右键, 0 = 左键, 2 = 中键
    public float yawSpeed = 180f;        // degrees per second (scaled by mouse delta)
    public bool lockCursorWhileRotating = true;

    [Header("Pitch (Fixed)")]
    [Range(10f, 80f)] public float pitchAngle = 45f; // 固定俯角

    [Header("Zoom (Mouse Wheel)")]
    public float zoomSpeed = 20f;
    public float minDistance = 8f;
    public float maxDistance = 60f;
    public float startDistance = 25f;

    [Header("Boundary Limits (Optional)")]
    [Tooltip("限制摄像机移动范围，防止移出地形")]
    public bool enableBoundary = false;
    public Vector2 boundaryMin = Vector2.zero;
    public Vector2 boundaryMax = new Vector2(100f, 100f);

    private float distance;
    private Vector3? targetPosition;  // 自定义目标位置
    private float moveToSpeed = 20f;  // 移动到目标位置的速度

    void Reset()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam != null) pitchPivot = cam.transform.parent;
    }

    void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam != null && pitchPivot == null) pitchPivot = cam.transform.parent;

        // 尝试自动修正层级：如果 camera 直接挂在 rig 下，则创建 pitchPivot
        if (cam != null && pitchPivot == transform)
        {
            var pivot = new GameObject("PitchPivot").transform;
            pivot.SetParent(transform, false);
            cam.transform.SetParent(pivot, true);
            pitchPivot = pivot;
        }

        distance = Mathf.Clamp(startDistance, minDistance, maxDistance);

        ApplyPitch();
        ApplyZoomImmediate();
    }

    void Update()
    {
        HandleMoveToTarget();
        HandleMoveXZ();
        HandleRotateYaw();
        HandleZoom();
    }

    private void HandleMoveToTarget()
    {
        if (!targetPosition.HasValue) return;

        // 移动到目标位置
        Vector3 currentPos = transform.position;
        Vector3 targetPos = targetPosition.Value;

        // 保持Y轴不变
        targetPos.y = currentPos.y;

        float distance = Vector3.Distance(currentPos, targetPos);
        if (distance < 0.1f)
        {
            transform.position = targetPos;
            targetPosition = null;  // 到达目标，清除目标
            return;
        }

        // 平滑移动
        transform.position = Vector3.MoveTowards(currentPos, targetPos, moveToSpeed * Time.deltaTime);
    }

    private void HandleMoveXZ()
    {
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;

        Vector3 input = new Vector3(x, 0f, z);
        if (input.sqrMagnitude < 0.0001f) return;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        // 使用当前 yaw 朝向的 forward/right（投影到 XZ）
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = transform.right; right.y = 0f; right.Normalize();

        Vector3 move = (right * input.x + fwd * input.z).normalized;
        Vector3 newPos = transform.position + move * speed * Time.deltaTime;

        // 应用边界限制
        if (enableBoundary)
        {
            newPos.x = Mathf.Clamp(newPos.x, boundaryMin.x, boundaryMax.x);
            newPos.z = Mathf.Clamp(newPos.z, boundaryMin.y, boundaryMax.y);
        }

        transform.position = newPos;
    }

    private void HandleRotateYaw()
    {
        if (!Input.GetMouseButton(rotateMouseButton))
        {
            if (lockCursorWhileRotating && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        if (lockCursorWhileRotating)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        float mx = Input.GetAxis("Mouse X"); // 旧输入轴，最简单好用
        float yawDelta = mx * yawSpeed * Time.deltaTime;

        transform.Rotate(0f, yawDelta, 0f, Space.World);
    }

    private void HandleZoom()
    {
        if (cam == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.0001f) return;

        distance -= scroll * zoomSpeed * Time.deltaTime * 10f; // *10 让滚轮手感更明显
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        ApplyZoomImmediate();
    }

    private void ApplyPitch()
    {
        if (pitchPivot == null) return;
        pitchPivot.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);
    }

    private void ApplyZoomImmediate()
    {
        if (cam == null || pitchPivot == null) return;

        // 摄像机沿 pitchPivot 的局部 -Z 方向后退 distance
        cam.transform.localPosition = new Vector3(0f, 0f, -distance);
        cam.transform.localRotation = Quaternion.identity;
    }

    // ============ Public Methods ============

    /// <summary>
    /// 移动摄像机到指定的世界坐标
    /// </summary>
    public void MoveTo(Vector3 worldPosition)
    {
        targetPosition = worldPosition;
    }

    /// <summary>
    /// 立即跳转到指定的世界坐标
    /// </summary>
    public void JumpTo(Vector3 worldPosition)
    {
        Vector3 pos = worldPosition;
        pos.y = transform.position.y;  // 保持当前Y轴高度
        transform.position = pos;
        targetPosition = null;
    }

    /// <summary>
    /// 取消当前的移动
    /// </summary>
    public void CancelMove()
    {
        targetPosition = null;
    }
}