using UnityEngine;

/// <summary>在摄像机可视范围内随机选点飞舞（StartScene 蚊子）。</summary>
public class StartSceneMosquitoFly : MonoBehaviour
{
    Camera _camera;
    float _margin;
    float _moveSpeed;
    float _renderZ;
    Vector3 _target;
    float _pickTargetDistance = 0.12f;

    public void Initialize(Camera camera, float boundsMargin, float moveSpeed, float renderZ)
    {
        _camera = camera != null ? camera : Camera.main;
        _margin = Mathf.Max(0f, boundsMargin);
        _moveSpeed = Mathf.Max(0.1f, moveSpeed);
        _renderZ = renderZ;
        transform.position = WithRenderZ(GetRandomPositionInView());
        PickNewTarget();
    }

    void Update()
    {
        if (_camera == null)
            _camera = Camera.main;
        if (_camera == null || !_camera.orthographic)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            _target,
            _moveSpeed * Time.deltaTime);

        if ((transform.position - _target).sqrMagnitude <= _pickTargetDistance * _pickTargetDistance)
            PickNewTarget();
    }

    void PickNewTarget()
    {
        _target = WithRenderZ(GetRandomPositionInView());
    }

    Vector3 WithRenderZ(Vector3 pos)
    {
        pos.z = _renderZ;
        return pos;
    }

    Vector3 GetRandomPositionInView()
    {
        GetViewBounds(out float minX, out float maxX, out float minY, out float maxY);
        float x = Random.Range(minX, maxX);
        float y = Random.Range(minY, maxY);
        return new Vector3(x, y, 0f);
    }

    void GetViewBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        float halfH = _camera.orthographicSize - _margin;
        float halfW = halfH * _camera.aspect - _margin;
        if (halfH < 0.1f) halfH = 0.1f;
        if (halfW < 0.1f) halfW = 0.1f;

        Vector3 c = _camera.transform.position;
        minX = c.x - halfW;
        maxX = c.x + halfW;
        minY = c.y - halfH;
        maxY = c.y + halfH;
    }
}
