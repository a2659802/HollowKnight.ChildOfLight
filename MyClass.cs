using HutongGames.PlayMaker;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            accelerationForce = Random.Range(40, 60);
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
                var best = MatchBest(targets);
                if (best == null)
                {
                    Modding.Logger.LogDebug("No Match!");
                    int selected = Random.Range(0, targets.Count);
                    target = targets[selected];
                }
                else
                {
                    target = best;
                }
            }

        }
        /*public static void debugPrint(HealthManager target)
        {
            Log($"[{target.name}][{target.isActiveAndEnabled},{target.gameObject.GetComponent<Collider2D>()?.isTrigger}]{target.hp},{target.hasSpecialDeath},{target.deathReset},{target.damageOverride},{target.isDead},{target.InvincibleFromDirection},{target.IsInvincible}");
        }*/
        private static bool searchActiveEnemy(HealthManager hm)
        {
            if (hm == null || (!hm.gameObject.activeSelf) || hm.isDead)
                return false;
            if (hm.IsInvincible)
                return false;
            if (hm.hp < 1)
                return false;
            return true;
        }
        private HealthManager MatchBest(List<HealthManager> targets)
        {
            if (targets.Count < 1)
                return null;
            else if (targets.Count == 1)
                return targets[0];
            else
            {

                float min_factor = 999 + 1*(-0.05f);
                HealthManager best = null;
                foreach(var hm in targets)
                {
                    var polordis = hm.transform.position - gameObject.transform.position;
                    var distance = polordis.x * polordis.x + polordis.y * polordis.y;
                    var hp = hm.hp;
                    var cur_factor = (-0.05f)*hp + distance;
                    var coll = hm.gameObject.GetComponent<Collider2D>();
                    if(coll && coll.isTrigger)
                    {
                        cur_factor += 100;
                    }
                    if(cur_factor>0 && cur_factor<min_factor && !hm.deathReset)
                    {
                        min_factor = cur_factor;
                        best = hm;
                    }
                    if(!hm.hasSpecialDeath)
                    {
                        return hm;
                    }
                }
                return best;
            }
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
