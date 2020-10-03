using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using GlobalEnums;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using ModCommon.Util;
using Modding;
using UnityEngine;
namespace ChildOfLight
{
    public class ChildOfLight : Mod,ITogglableMod
    {
        GameObject orbPre;
        GameObject ShotCharge;
        GameObject ShotCharge2;
        GameObject BeamSweeper;
        GameObject HKBlast;
        GameObject SpikePre;
        GameObject SpikeCenter;
        private int setupDone = 0;
        private readonly List<GameObject> _spikes = new List<GameObject>();

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            #region GetPrefab
            
            var abs = preloadedObjects["GG_Radiance"]["Boss Control/Absolute Radiance"];
            var fsm = abs.LocateMyFSM("Attack Commands");
            var spawnAction = fsm.GetAction<SpawnObjectFromGlobalPool>("Spawn Fireball", 1);
            orbPre = UnityEngine.Object.Instantiate(spawnAction.gameObject.Value, null);
            UnityEngine.Object.DontDestroyOnLoad(orbPre);
            orbPre.SetActive(false);
            ShotCharge = abs.transform.Find("Shot Charge").gameObject;
            ShotCharge2 = abs.transform.Find("Shot Charge 2").gameObject;
            var finalcontrol = orbPre.LocateMyFSM("Final Control");
            UnityEngine.Object.DestroyImmediate(finalcontrol);
            var herohurter = orbPre.transform.Find("Hero Hurter").GetComponent<DamageHero>();
            UnityEngine.Object.DestroyImmediate(herohurter);
            BeamSweeper = preloadedObjects["GG_Radiance"]["Boss Control/Beam Sweeper"];
            BeamSweeper.transform.SetParent(null);

            HKBlast = preloadedObjects["GG_Hollow_Knight"]["Battle Scene/Focus Blasts/HK Prime Blast"];

            SpikePre = preloadedObjects["GG_Radiance"]["Boss Control/Spike Control/Far L/Radiant Spike"];
            
            #endregion

            #region Setup
            GameManager.instance.StartCoroutine(WaitHero(() => {
                SetupOrb();
                SetupBeam();
                SetupBlast();
                SetupSpike();
            }));
            #endregion

            #region Hooks
            ModHooks.Instance.AfterSavegameLoadHook += ApplyTrigger;
            GameManager.instance.StartCoroutine(WaitSetup(()=> ModHooks.Instance.CharmUpdateHook += Instance_CharmUpdateHook));
            #endregion

 
        }


        private void ApplyTrigger(SaveGameData data)
        {
            GameManager.instance.StartCoroutine(WaitHero(() => SetupTrigger()));
        }

        private IEnumerator WaitHero(Action a)
        {
            yield return new WaitWhile(() => HeroController.instance == null);
            yield return new WaitForSeconds(0.3f);
            a?.Invoke();
        }
        private IEnumerator WaitSetup(Action a)
        {
            while (setupDone < 4)
            {
                yield return null;
            }
               
            LogDebug("All ability Setup Donw");
            a?.Invoke();
        }
        public void Unload()
        {
            ModHooks.Instance.AfterSavegameLoadHook -= ApplyTrigger;
            ModHooks.Instance.CharmUpdateHook -= Instance_CharmUpdateHook;
        }

        public override string GetVersion()
        {
            Assembly asm = Assembly.GetExecutingAssembly();

            string ver = asm.GetName().Version.ToString();

            using SHA1 sha1 = SHA1.Create();
            using FileStream stream = File.OpenRead(asm.Location);

            byte[] hashBytes = sha1.ComputeHash(stream);

            string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            return $"{ver}-{hash.Substring(0, 6)}";
        }
        private void SetupTrigger()
        {
            var spellctrl = HeroController.instance.spellControl;
            spellctrl.InsertMethod("Spore Cloud 2", 0, () => {
                HKBlast.transform.position = HeroController.instance.transform.position;
                HKBlast.LocateMyFSM("Control").SetState("Blast");
            });
            spellctrl.InsertMethod("Spore Cloud", 0, () => {
                HKBlast.transform.position = HeroController.instance.transform.position;
                HKBlast.LocateMyFSM("Control").SetState("Blast");
            });

            spellctrl.InsertAction("Scream Get?", new EndAction(() => {
                //FSMUtility.SendEventToGameObject(BeamSweeper, "BEAM SWEEP R2");
                var beamctrl = BeamSweeper.LocateMyFSM("Control");
                beamctrl.SetState("Beam Sweep R 2");
                HeroController.instance.TakeMP(33);
                spellctrl.SetState("Inactive");
            }), 0);

            spellctrl.InsertAction("Has Fireball?", new EndAction(() => {
                HeroController.instance.StartCoroutine(SpawnOrb());
                HeroController.instance.TakeMP(33);
                spellctrl.SetState("Inactive");
            }), 0);

            spellctrl.InsertMethod("Q2 Land", 0, () => {
                int n = 10;
                float spacing = 0.8f;
                int dmgAmount = 5;
                Vector3 scale = new Vector3(1.0f, 0.7f, 0.9f);
                Vector3 pos = HeroController.instance.transform.position;
                if (PlayerData.instance.equippedCharm_15)
                {
                    n += 5;
                }
                if (PlayerData.instance.equippedCharm_19)
                {
                    spacing *= 2;
                }
                if (PlayerData.instance.equippedCharm_12)
                {
                    dmgAmount += 5;
                }
                if (PlayerData.instance.equippedCharm_21)
                {
                    dmgAmount -= 3;
                }
                if (PlayerData.instance.equippedCharm_16)
                {
                    scale.y *= 2;
                    scale.x *= 1.2f;
                    dmgAmount -= 1;
                    pos.y += 1.1f;
                }
                if ((PlayerData.instance.equippedCharm_8) || (PlayerData.instance.equippedCharm_9) || (PlayerData.instance.equippedCharm_27))
                {
                    n += (PlayerData.instance.healthBlue / 2);
                }
                SpikePre.transform.localScale = scale;
                SpikePre.GetComponent<DamageEnemies>().damageDealt = dmgAmount;
                SpikeCenter.transform.position = pos;
                SpawnSpike(n, spacing);
                HeroController.instance.TakeMP(10);
            });

            spellctrl.Fsm.SaveActions(); 

        }
        private void SetupOrb()
        {
            var orb = orbPre;
            orb.layer = 17;   //PhysLayers.HERO_ATTACK
            AddDamageEnemy(orb);

            var orbcontrol = orb.LocateMyFSM("Orb Control");

            FsmState chaseEnemy = new FsmState(orbcontrol.Fsm)
            {
                Name = "Chase Enemy"
            };
            #region Event&Transition
            FsmEvent hitevent = new FsmEvent("ORBHIT");
            FsmEvent dispateevent = FsmEvent.GetFsmEvent("DISSIPATE");
            orbcontrol.ChangeTransition("Init", "FIRE", "Chase Enemy");
            orbcontrol.ChangeTransition("Init", "FINISHED", "Chase Enemy");
            chaseEnemy.Transitions = new FsmTransition[]
            {
                new FsmTransition
                {
                    FsmEvent = hitevent,
                    ToState = "Impact pause"
                },
                new FsmTransition
                {
                    FsmEvent = dispateevent,
                    ToState = "Dissipate"
                },
            };
            #endregion

            var _list = orbcontrol.FsmStates.ToList();
            _list.Add(chaseEnemy);
            var _toremove = new List<FsmState>();
            foreach (var s in _list)
            {
                if (s.Name == "Orbiting" || s.Name.Contains("Chase Hero"))
                    _toremove.Add(s);
            }
            foreach (var s in _toremove)
            {
                _list.Remove(s);
            }

            orbcontrol.Fsm.States = _list.ToArray();

            chaseEnemy.Actions = new FsmStateAction[]
            {
                new Trigger2dEventLayer{
                    trigger =  PlayMakerUnity2d.Trigger2DType.OnTriggerEnter2D,
                    collideLayer = 11,
                    sendEvent = hitevent,
                    collideTag = "",
                    storeCollider = new FsmGameObject()
        },
                new Trigger2dEventLayer{
                    trigger =  PlayMakerUnity2d.Trigger2DType.OnTriggerStay2D,
                    collideLayer = 11,
                    sendEvent = hitevent,
                    collideTag = "",
                    storeCollider = new FsmGameObject()
        },
                new Wait
                {
                    time = 3.5f,
                    finishEvent = dispateevent
                },
            };
            //orbcontrol.SetState("Chase Enemy");

            orbcontrol.GetAction<Wait>("Impact", 7).time = 0.1f;
            //orb.AddComponent<MyChaseObject>();
            orbcontrol.Fsm.SaveActions();

            setupDone++;
        }
        private void SetupBlast()
        {
            //HKBlast.transform.SetParent(HeroController.instance.transform);
            HKBlast.transform.position = HeroController.instance.transform.position;
            GameObject blast;
            var fsm = HKBlast.LocateMyFSM("Control");
            var blastAction = fsm.GetAction<ActivateGameObject>("Blast", 0);
            blast = UnityEngine.Object.Instantiate(blastAction.gameObject.GameObject.Value);
            blast.name = "MyBlast";
            UnityEngine.Object.DontDestroyOnLoad(blast);
            blast.transform.SetParent(HKBlast.transform);
            blast.transform.localPosition = new Vector3(0, 0, 0);
            blast.SetActive(false);
            var damager = blast.transform.Find("hero_damager");
            UnityEngine.Object.DestroyImmediate(damager.GetComponent<DamageHero>());
            HKBlast.layer = (int)PhysLayers.HERO_ATTACK;
            blast.layer = (int)PhysLayers.HERO_ATTACK;
            damager.gameObject.layer = (int)PhysLayers.HERO_ATTACK;

            //AddDamageEnemy(HKBlast);
            AddDamageEnemy(damager.gameObject).circleDirection = true;

            blastAction.gameObject.GameObject.Value = blast;
            fsm.Fsm.SaveActions();

            var hkblastfsm = HKBlast.LocateMyFSM("Control");
            hkblastfsm.InsertMethod("Blast", 0, () => {
                Vector3 scale = new Vector3(1, 1, 1);
                MaterialPropertyBlock prop = new MaterialPropertyBlock();
                Color color = new Color(1, 1, 1);
                if (PlayerData.instance != null)
                {
                    if (PlayerData.instance.equippedCharm_34)
                    {
                        scale.x *= 3;
                        scale.y *= 3;
                    }
                    if (PlayerData.instance.equippedCharm_28)
                    {
                        scale.y *= 0.5f;
                        scale.x *= 1.3f;
                    }
                    if (PlayerData.instance.equippedCharm_10)
                    {
                        color.r += 1;
                        color.g += 1;
                        color.b -= 1;
                    }
                    if (PlayerData.instance.healthBlue > 0)
                    {
                        color.b += 1;
                    }

                }

                HKBlast.transform.localScale = scale;

                prop.SetColor("_Color", color);
                foreach (Transform t in blast.transform)
                {
                    var render = t.GetComponent<SpriteRenderer>();
                    if (render != null)
                    {
                        render.SetPropertyBlock(prop);
                    }
                }

            });
            var idle = hkblastfsm.GetState("Idle");
            idle.Transitions = new FsmTransition[] { };
            hkblastfsm.Fsm.SaveActions();
            HKBlast.SetActive(true);

            setupDone++;
        }
        private void SetupBeam()
        {
            var beamctrl = BeamSweeper.LocateMyFSM("Control");

            var spawnbeam = beamctrl.GetAction<SpawnObjectFromGlobalPoolOverTime>("Beam Sweep R 2", 5);
            var _beampre = spawnbeam.gameObject.Value;
            var beamPre = UnityEngine.Object.Instantiate(_beampre);
            UnityEngine.Object.DontDestroyOnLoad(beamPre);
            beamPre.SetActive(false);
            UnityEngine.Object.DestroyImmediate(beamPre.GetComponent<DamageHero>());
            AddDamageEnemy(beamPre).direction = 90;
            spawnbeam.gameObject.Value = beamPre;
            BeamSweeper.layer = (int)PhysLayers.HERO_ATTACK;
            beamPre.layer = (int)PhysLayers.HERO_ATTACK;
            var myspawnbeam = new MySpawnObjectFromGlobalPoolOverTime
            {
                gameObject = spawnbeam.gameObject,
                spawnPoint = spawnbeam.spawnPoint,
                position = new Vector3(0, 0, 0),
                rotation = new Vector3(0, 0, 0),
                frequency = 0.075f
            };
            beamctrl.RemoveAction("Beam Sweep R 2", 4);
            beamctrl.InsertMethod("Beam Sweep R 2", 4, () => {
                if (HeroController.instance != null)
                {
                    Vector3 heropos = HeroController.instance.transform.position;
                    heropos.y -= 10;
                    heropos.x -= 30;
                    BeamSweeper.transform.position = heropos;
                }
            });
            beamctrl.RemoveAction("Beam Sweep R 2", 5);
            beamctrl.InsertAction("Beam Sweep R 2", myspawnbeam, 5);

            beamctrl.GetAction<iTweenMoveBy>("Beam Sweep R 2", 6).vector = new Vector3(0, 50, 0);

            var idle = beamctrl.GetState("Idle");
            idle.Transitions = new FsmTransition[] { };
            beamctrl.Fsm.SaveActions();
            BeamSweeper.SetActive(true);

            /*try
            {
                FSMUtility.SendEventToGameObject(BeamSweeper, "BEAM SWEEP R2");
            }
            catch
            {

            }*/
            setupDone++;
        }
        private void SetupSpike()
        {
            UnityEngine.Object.DestroyImmediate(SpikePre.LocateMyFSM("Hero Saver"));
            UnityEngine.Object.DestroyImmediate(SpikePre.GetComponent<DamageHero>());
            AddDamageEnemy(SpikePre).damageDealt = 5;

            var spikectrl = SpikePre.LocateMyFSM("Control");
            spikectrl.RemoveTransition("Up", "DOWN");
            spikectrl.RemoveTransition("Up", "SPIKES DOWN");
            spikectrl.InsertAction("Up", new Wait { time = 0.4f, finishEvent = FsmEvent.Finished }, 0);
            var downed = spikectrl.GetState("Downed");
            var floor_antic = spikectrl.GetState("Floor Antic");
            var spike_up = spikectrl.GetState("Spike Up");
            var up = spikectrl.GetState("Up");

            downed.Transitions = new FsmTransition[] { };
            floor_antic.Transitions = new FsmTransition[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = "Spike Up" } };
            spike_up.Transitions = new FsmTransition[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = "Up" } };
            up.Transitions = new FsmTransition[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = "Down" } };
            spikectrl.AddTransition("Downed", "HEROSPIKEUP", "Floor Antic");

            spikectrl.Fsm.SaveActions();
            SpikeCenter = new GameObject { name = "HeroSpikeCenter", layer = 23, active = true };
            UnityEngine.Object.DontDestroyOnLoad(SpikeCenter);
            SpikeCenter.transform.position = HeroController.instance.transform.position;

            setupDone++;
        }
        public IEnumerator SpawnOrb()
        {


            var spawnPoint = new Vector3(HeroController.instance.transform.position.x + UnityEngine.Random.Range(-2, 2), HeroController.instance.transform.position.y + 2 + UnityEngine.Random.Range(-3, 2));
            var ShotCharge = GameObject.Instantiate(this.ShotCharge);
            var ShotCharge2 = GameObject.Instantiate(this.ShotCharge2);
            ShotCharge.transform.position = spawnPoint;
            ShotCharge2.transform.position = spawnPoint;
            ShotCharge.SetActive(true);
            ShotCharge2.SetActive(true);
            var em = ShotCharge.GetComponent<ParticleSystem>().emission;
            var em2 = ShotCharge2.GetComponent<ParticleSystem>().emission;
            em.enabled = true;
            em2.enabled = true;

            if(PlayerData.instance!=null&&PlayerData.instance.equippedCharm_33)
            {
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                yield return new WaitForSeconds(0.8f);
            }
            var orb = orbPre.Spawn(spawnPoint);

            orb.AddComponent<OrbChaseObject>();
            if (PlayerData.instance != null)
            {
                if (PlayerData.instance.equippedCharm_11)
                {
                    //MaterialPropertyBlock _propblock = new MaterialPropertyBlock();
                    var scale = orb.transform.localScale;
                    scale.x *= 0.5f;
                    scale.y *= 0.5f;
                    yield return new WaitForSeconds(0.5f);
                    var another_orb = orbPre.Spawn(spawnPoint);
                    //_propblock.SetColor("_Color", Color.blue);
                    //another_orb.GetComponent<Renderer>().SetPropertyBlock(_propblock);
                    another_orb.AddComponent<OrbChaseObject>();
                    another_orb.SetActive(true);
                    another_orb.transform.localScale = scale;
                    yield return new WaitForSeconds(0.3f);
                    another_orb = orbPre.Spawn(spawnPoint);
                    //_propblock.SetColor("_Color", Color.green);
                    //another_orb.GetComponent<Renderer>().SetPropertyBlock(_propblock);
                    another_orb.AddComponent<OrbChaseObject>();
                    another_orb.SetActive(true);
                    another_orb.transform.localScale = scale;

                }
            }
            orb.SetActive(true);

            em.enabled = false;
            em2.enabled = false;
        }
        private void SpawnSpike(int n = 10, float spacing = 0.8f)
        {
            AddSpikeToPool(n, spacing);
            foreach (var s in _spikes)
            {
                s.LocateMyFSM("Control").SendEvent("HEROSPIKEUP");
            }
        }
        private bool AddSpikeToPool(int n = 10, float spacing = 0.8f)
        {
            /*foreach (var s in _spikes)
                UnityEngine.Object.Destroy(s);*/
            _spikes.Clear();

            float x = -1 * ((n * spacing) / 2);
            for (int i = 0; i < n; i++)
            {
                GameObject s = UnityEngine.Object.Instantiate(SpikePre);
                s.transform.SetParent(SpikeCenter.transform);
                s.transform.localPosition = new Vector3(x, -0.4f, 0);
                x += spacing;
                _spikes.Add(s);
                s.SetActive(true);
            }
            return true;
        }
        public DamageEnemies AddDamageEnemy(GameObject go)
        {
            //var template = HeroController.instance.slashAltPrefab.LocateMyFSM("damages_enemy");

            //AddFsmBySource(go, template);
            var dmg = go.AddComponent<DamageEnemies>();
            dmg.attackType = AttackTypes.Spell;
            dmg.circleDirection = false;
            dmg.damageDealt = 20;
            dmg.direction = 90 * 3;
            dmg.ignoreInvuln = false;
            dmg.magnitudeMult = 1f;
            dmg.moveDirection = false;
            dmg.specialType = 0;

            return dmg;
        }
        private void Instance_CharmUpdateHook(PlayerData data, HeroController controller)
        {
            if (!PlayerData.instance.equippedCharm_21)
            {
                orbPre.GetComponent<DamageEnemies>().attackType = AttackTypes.Nail;
                orbPre.GetComponent<DamageEnemies>().magnitudeMult = 2f;
            }
            else
            {
                orbPre.GetComponent<DamageEnemies>().attackType = AttackTypes.Spell;
            }

            if (PlayerData.instance.equippedCharm_19) // 萨满
            {

                var beamctrl = BeamSweeper.LocateMyFSM("Control");
                beamctrl.GetAction<iTweenMoveBy>("Beam Sweep R 2", 6).vector = new Vector3(0, 50 * 0.7f, 0);

                MaterialPropertyBlock propblock;
                orbPre.GetComponent<DamageEnemies>().damageDealt = 30;
                propblock = new MaterialPropertyBlock();
                var c = new Color(0.05f, 0.05f, 0.05f);
                propblock.SetColor("_Color", c);
                orbPre.GetComponent<Renderer>().SetPropertyBlock(propblock);
                var scale = new Vector3(1.4f, 1.4f, 1);
                scale.x *= 1.2f;
                scale.y *= 1.2f;
                orbPre.transform.localScale = scale;

            }
            else
            {

                var beamctrl = BeamSweeper.LocateMyFSM("Control");
                beamctrl.GetAction<iTweenMoveBy>("Beam Sweep R 2", 6).vector = new Vector3(0, 50, 0);

                MaterialPropertyBlock propblock;
                orbPre.GetComponent<DamageEnemies>().damageDealt = 20;
                propblock = new MaterialPropertyBlock();
                propblock.SetColor("_Color", Color.white);
                orbPre.GetComponent<Renderer>().SetPropertyBlock(propblock);
                var scale = new Vector3(1.4f, 1.4f, 1);
                orbPre.transform.localScale = scale;

            }

        }
        public override List<(string, string)> GetPreloadNames()
        {
            return new List<(string, string)>
            {
                ("GG_Radiance","Boss Control/Absolute Radiance"),
                ("GG_Radiance","Boss Control/Beam Sweeper"),
                ("GG_Radiance","Boss Control/Spike Control/Far L/Radiant Spike"),
                ("GG_Hollow_Knight","Battle Scene/Focus Blasts/HK Prime Blast"),
            };
        }
    }

    class EndAction : FsmStateAction
    {
        private readonly Action _a;
        public EndAction(Action a)
        {
            _a = a;
        }
        public override void OnEnter()
        {
            _a?.Invoke();
        }
    }
}
