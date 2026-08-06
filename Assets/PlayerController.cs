using UnityEngine;
using UnityEngine.InputSystem; // 新版输入系统命名空间

public class PlayerController : MonoBehaviour
{
    [Header("跳跃参数")]
    public float jumpForce = 30f; // 增大了默认力度
    
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 1. 按下空格键触发跳跃
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Jump();
        }

        // 2. 马里奥式手感：如果松开空格且正在上升，减小垂直速度
        // 这会让下落感更重、更干脆，避免角色“浮在空中”
        if (Keyboard.current.spaceKey.wasReleasedThisFrame && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }
    }

    void Jump()
    {
        // 确保清除当前的垂直速度，让每次跳跃的力度一致
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        
        // 施加瞬间冲击力
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }
}