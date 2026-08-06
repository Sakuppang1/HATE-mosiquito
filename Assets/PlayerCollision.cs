using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    public float bounceForce = 5f; // 单词块被顶起来的力度

    // 使用 OnCollisionEnter2D 而不是 OnTriggerEnter2D
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 1. 检查是否碰到了单词块
        if (collision.gameObject.CompareTag("Syllable"))
        {
            // 2. 只有当角色是从下往上顶时（通过检测垂直方向的碰撞点）才触发
            // 如果玩家中心点在单词块中心点下方，才是“顶”
            if (transform.position.y < collision.transform.position.y)
            {
                HandleScoreAndBounce(collision.gameObject);
            }
        }
    }

    private void HandleScoreAndBounce(GameObject block)
    {
        // 得分仅由声控吃掉（CenterZoneController）按重音判定，此处只保留顶撞物理效果

        // 4. 实现单词块的“向上弹起”效果
        Rigidbody2D blockRb = block.GetComponent<Rigidbody2D>();
        if (blockRb != null)
        {
            // 临时将 BodyType 改为 Dynamic 才能受力，如果是 Kinematic 也可以手动改位移
            blockRb.bodyType = RigidbodyType2D.Dynamic; 
            blockRb.linearVelocity = Vector2.zero; // 清除原有速度
            blockRb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);
        }
    }
}