using HutongGames.PlayMaker;
using System.Collections;
using System.Linq;
using UnityEngine;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

namespace ChildOfLight
{
    public class OrbChaseObject : MonoBehaviour
    {
        public HealthManager target;
        public float accelerationForce;
        public float speedMax;
        private Rigidbody2D rb2d;
        private void Awake()
        {
            rb2d = GetComponent<Rigidbody2D>();
            if (rb2d == null)
            {
                Destroy(gameObject);
                return;
            }
            var targets = FindObjectsOfType<HealthManager>().ToList();
            speedMax = Random.Range(20, 30);
            accelerationForce = Random.Range(40, 70);
            for(int i=0;i<targets.ToArray().Length;i++)
            {
                var t = targets[i];
                if (!searchActiveEnemy(t))
                {
                    targets.Remove(t);
                }
            }
            if(targets.Count>0)
            {
                int selected = Random.Range(0, targets.Count);
                for (int i = 0; i < targets.Count; i++)
                {
                    if (!(targets[i].deathReset))
                    {
                        selected = i;
                    }
                }
                target = targets[selected];
            }

        }
        private static bool searchActiveEnemy(HealthManager hm)
        {
            if (hm == null || (!hm.gameObject.activeSelf) || hm.isDead)
                return false;
            if (hm.IsInvincible)
                return false;
            return true;
        }
        private IEnumerator Start()
        {
            yield return new WaitUntil(() =>
            {
                if (target != null && target.gameObject.activeSelf && !target.isDead && !target.IsInvincible)
                {
                    return true;
                }
                target = FindObjectOfType<HealthManager>();
                return false;
            });
            rb2d.bodyType = 0;
        }
        private void FixedUpdate()
        {
            SetChase(rb2d, target, accelerationForce, speedMax);
        }
        public static void SetChase(Rigidbody2D rb2d, HealthManager target, float accelerationForce, float speedMax)
        {
            if (target == null || rb2d == null)
                return;

            Vector2 vector = new Vector2(target.transform.position.x - rb2d.transform.position.x, target.transform.position.y - rb2d.transform.position.y);
            vector = Vector2.ClampMagnitude(vector, 1f);
            vector = new Vector2(vector.x * accelerationForce, vector.y * accelerationForce);
            rb2d.AddForce(vector);
            Vector2 vector2 = rb2d.velocity;
            vector2 = Vector2.ClampMagnitude(vector2, speedMax);
            rb2d.velocity = vector2;
        }
        internal static void Log(object msg) => Modding.Logger.Log($"[OrbChaseObject]:{msg}");
    }

    public class MySpawnObjectFromGlobalPoolOverTime : FsmStateAction
    {
        public override void Reset()
        {
            this.gameObject = null;
            this.spawnPoint = null;
            this.position = new FsmVector3
            {
                UseVariable = true
            };
            this.rotation = new FsmVector3
            {
                UseVariable = true
            };
            this.frequency = null;
        }
        public override void OnUpdate()
        {
            this.timer += Time.deltaTime;
            if (this.timer >= this.frequency.Value)
            {
                this.timer = 0f;
                GameObject value = this.gameObject.Value;
                if (value != null)
                {
                    Vector3 a = Vector3.zero;
                    Vector3 euler = Vector3.up;
                    if (this.spawnPoint.Value != null)
                    {
                        a = this.spawnPoint.Value.transform.position;
                        if (!this.position.IsNone)
                        {
                            a += this.position.Value;
                        }
                        euler = (this.rotation.IsNone ? this.spawnPoint.Value.transform.eulerAngles : this.rotation.Value);
                    }
                    else
                    {
                        if (!this.position.IsNone)
                        {
                            a = this.position.Value;
                        }
                        if (!this.rotation.IsNone)
                        {
                            euler = this.rotation.Value;
                        }
                    }
                    if (this.gameObject != null)
                    {
                        try
                        {
                            GameObject gameObject = this.gameObject.Value.Spawn(a, Quaternion.Euler(euler));
                            gameObject.SetActive(true);
                        }
                        catch
                        {
                            //Modding.Logger.LogDebug("Ignore this exception");
                        }
                    }
                }
            }
        }
        [Tooltip("GameObject to create. Usually a Prefab.")]
        [RequiredField]
        public FsmGameObject gameObject;

        // Token: 0x04000F72 RID: 3954
        [Tooltip("Optional Spawn Point.")]
        public FsmGameObject spawnPoint;

        // Token: 0x04000F73 RID: 3955
        [Tooltip("Position. If a Spawn Point is defined, this is used as a local offset from the Spawn Point position.")]
        public FsmVector3 position;

        // Token: 0x04000F74 RID: 3956
        [Tooltip("Rotation. NOTE: Overrides the rotation of the Spawn Point.")]
        public FsmVector3 rotation;

        // Token: 0x04000F75 RID: 3957
        [Tooltip("How often, in seconds, spawn occurs.")]
        public FsmFloat frequency;

        // Token: 0x04000F76 RID: 3958
        private float timer;
    }
}
