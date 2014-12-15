using System;
using System.Linq;
using System.Collections.Generic;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace MasterSeries.Common
{
    class M_Orbwalker
    {
        private static readonly string[] AttackResets =
        {
            "dariusnoxiantacticsonh", "fioraflurry", "garenq", "hecarimrapidslash", "jaxempowertwo", "jaycehypercharge", "leonashieldofdaybreak",
            "luciane", "monkeykingdoubleattack", "mordekaisermaceofspades", "nasusq", "nautiluspiercinggaze", "netherblade", "parley", "poppydevastatingblow",
            "powerfist", "renektonpreexecute", "rengarq", "shyvanadoubleattack", "sivirw", "takedown", "talonnoxiandiplomacy", "trundletrollsmash",
            "vaynetumble", "vie", "volibearq", "xenzhaocombotarget", "yorickspectral"
        };
        private static readonly string[] NoAttacks =
        {
            "jarvanivcataclysmattack", "monkeykingdoubleattack", "shyvanadoubleattack", "shyvanadoubleattackdragon", "zyragraspingplantattack",
            "zyragraspingplantattack2", "zyragraspingplantattackfire", "zyragraspingplantattack2fire"
        };
        private static readonly string[] Attacks =
        {
            "caitlynheadshotmissile", "frostarrow", "garenslash2", "kennenmegaproc", "lucianpassiveattack", "masteryidoublestrike", "quinnwenhanced",
            "renektonexecute", "renektonsuperexecute", "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust", "xenzhaothrust2", "xenzhaothrust3"
        };

        private static Menu Config;
        private static Obj_AI_Hero Player = ObjectManager.Player;
        public static Obj_AI_Base ForcedTarget = null;
        private static IEnumerable<Obj_AI_Hero> AllEnemys = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy);
        private static IEnumerable<Obj_AI_Hero> AllAllys = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsAlly);
        public enum Mode
        {
            Combo,
            Harass,
            LaneClear,
            LaneFreeze,
            LastHit,
            Flee,
            None
        }
        private static bool Attack = true;
        private static bool Move = true;
        private static bool DisableNextAttack;
        private const float ClearWaitTimeMod = 2f;
        private static int LastAttack;
        private static Obj_AI_Base LastTarget;
        private static Obj_AI_Minion PrevMinion;
        private static Spell MovePrediction;
        private static int LastMove;
        private static int WindUp;
        private static int LastRealAttack;

        public class BeforeAttackEventArgs
        {
            public Obj_AI_Base Target;
            public Obj_AI_Base Unit = ObjectManager.Player;
            private bool Value = true;
            public bool Process
            {
                get
                {
                    return Value;
                }
                set
                {
                    DisableNextAttack = !value;
                    Value = value;
                }
            }
        }
        public delegate void BeforeAttackEvenH(BeforeAttackEventArgs Args);
        public delegate void OnAttackEvenH(Obj_AI_Base Unit, Obj_AI_Base Target);
        public delegate void AfterAttackEvenH(Obj_AI_Base Unit, Obj_AI_Base Target);
        public delegate void OnTargetChangeH(Obj_AI_Base OldTarget, Obj_AI_Base NewTarget);
        public delegate void OnNonKillableMinionH(Obj_AI_Base Minion);

        public static event BeforeAttackEvenH BeforeAttack;
        public static event OnAttackEvenH OnAttack;
        public static event AfterAttackEvenH AfterAttack;
        public static event OnTargetChangeH OnTargetChange;
        public static event OnNonKillableMinionH OnNonKillableMinion;

        public static void AddToMenu(Menu menu)
        {
            var OWMenu = new Menu("Orbwalker", "OW");
            {
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    DrawMenu.AddItem(new MenuItem("OW_Draw_AARange", "AA Circle").SetValue(new Circle(true, Color.FloralWhite)));
                    DrawMenu.AddItem(new MenuItem("OW_Draw_AARangeEnemy", "AA Circle Enemy").SetValue(new Circle(true, Color.Pink)));
                    DrawMenu.AddItem(new MenuItem("OW_Draw_HoldZone", "Hold Zone").SetValue(new Circle(true, Color.FloralWhite)));
                    DrawMenu.AddItem(new MenuItem("OW_Draw_LastHit", "Minion Last Hit").SetValue(new Circle(true, Color.Lime)));
                    DrawMenu.AddItem(new MenuItem("OW_Draw_NearKill", "Minion Near Kill").SetValue(new Circle(true, Color.Gold)));
                    OWMenu.AddSubMenu(DrawMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    MiscMenu.AddItem(new MenuItem("OW_Misc_HoldZone", "Hold Zone").SetValue(new Slider(50, 100, 0)));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_FarmDelay", "Farm Delay").SetValue(new Slider(0, 200, 0)));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_ExtraWindUp", "Extra WindUp Time").SetValue(new Slider(80, 200, 0)));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_AutoWindUp", "Auto WindUp").SetValue(true));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_PriorityUnit", "Priority Unit").SetValue(new StringList(new[] { "Minion", "Hero" })));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_Humanizer", "Humanizer Delay").SetValue(new Slider(80, 200, 15)));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_MeleePrediction", "Melee Movement Prediction").SetValue(false));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_AllMovementDisabled", "Disable All Movement").SetValue(false));
                    MiscMenu.AddItem(new MenuItem("OW_Misc_AllAttackDisabled", "Disable All Attack").SetValue(false));
                    OWMenu.AddSubMenu(MiscMenu);
                }
                var ModeMenu = new Menu("Mode", "Mode");
                {
                    var ComboMenu = new Menu("Combo", "Mode_Combo");
                    {
                        ComboMenu.AddItem(new MenuItem("OW_Combo_Key", "Key").SetValue(new KeyBind(32, KeyBindType.Press)));
                        ComboMenu.AddItem(new MenuItem("OW_Combo_Move", "Movement").SetValue(true));
                        ComboMenu.AddItem(new MenuItem("OW_Combo_Attack", "Attack").SetValue(true));
                        ModeMenu.AddSubMenu(ComboMenu);
                    }
                    var HarassMenu = new Menu("Harass", "Mode_Harass");
                    {
                        HarassMenu.AddItem(new MenuItem("OW_Harass_Key", "Key").SetValue(new KeyBind("V".ToCharArray()[0], KeyBindType.Press)));
                        HarassMenu.AddItem(new MenuItem("OW_Harass_Move", "Movement").SetValue(true));
                        HarassMenu.AddItem(new MenuItem("OW_Harass_Attack", "Attack").SetValue(true));
                        HarassMenu.AddItem(new MenuItem("OW_Harass_LastHit", "Last Hit Minions").SetValue(true));
                        ModeMenu.AddSubMenu(HarassMenu);
                    }
                    var ClearMenu = new Menu("Lane/Jungle Clear", "Mode_Clear");
                    {
                        ClearMenu.AddItem(new MenuItem("OW_Clear_Key", "Key").SetValue(new KeyBind("G".ToCharArray()[0], KeyBindType.Press)));
                        ClearMenu.AddItem(new MenuItem("OW_Clear_Move", "Movement").SetValue(true));
                        ClearMenu.AddItem(new MenuItem("OW_Clear_Attack", "Attack").SetValue(true));
                        ModeMenu.AddSubMenu(ClearMenu);
                    }
                    var FreezeMenu = new Menu("Lane/Jungle Freeze", "Mode_Freeze");
                    {
                        FreezeMenu.AddItem(new MenuItem("OW_Freeze_Key", "Key").SetValue(new KeyBind("Y".ToCharArray()[0], KeyBindType.Press)));
                        FreezeMenu.AddItem(new MenuItem("OW_Freeze_Move", "Movement").SetValue(true));
                        FreezeMenu.AddItem(new MenuItem("OW_Freeze_Attack", "Attack").SetValue(true));
                        ModeMenu.AddSubMenu(FreezeMenu);
                    }
                    var LastHitMenu = new Menu("Last Hit", "Mode_LastHit");
                    {
                        LastHitMenu.AddItem(new MenuItem("OW_LastHit_Key", "Key").SetValue(new KeyBind(17, KeyBindType.Press)));
                        LastHitMenu.AddItem(new MenuItem("OW_LastHit_Move", "Movement").SetValue(true));
                        LastHitMenu.AddItem(new MenuItem("OW_LastHit_Attack", "Attack").SetValue(true));
                        ModeMenu.AddSubMenu(LastHitMenu);
                    }
                    var FleeMenu = new Menu("Flee", "Mode_Flee");
                    {
                        FleeMenu.AddItem(new MenuItem("OW_Flee_Key", "Key").SetValue(new KeyBind("C".ToCharArray()[0], KeyBindType.Press)));
                        ModeMenu.AddSubMenu(FleeMenu);
                    }
                    OWMenu.AddSubMenu(ModeMenu);
                }
                OWMenu.AddItem(new MenuItem("OW_Info", "Credits: xSLx & Esk0r"));
                Config = OWMenu;
                menu.AddSubMenu(OWMenu);
            }
            MovePrediction = new Spell(SpellSlot.Unknown, GetAutoAttackRange());
            MovePrediction.SetTargetted(Player.BasicAttack.SpellCastTime, Player.BasicAttack.MissileSpeed);
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Game.OnGameProcessPacket += OnGameProcessPacket;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            GameObject.OnCreate += OnCreateObjMissile;
        }

        private static void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || CurrentMode == Mode.None || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            CheckAutoWindUp();
            Orbwalk(Game.CursorPos, GetPossibleTarget());
        }

        private static void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item("OW_Draw_AARange").GetValue<Circle>().Active) Utility.DrawCircle(Player.Position, GetAutoAttackRange(), Config.Item("OW_Draw_AARange").GetValue<Circle>().Color);
            if (Config.Item("OW_Draw_AARangeEnemy").GetValue<Circle>().Active)
            {
                foreach (var Obj in AllEnemys.Where(i => Program.IsValid(i, 1500))) Utility.DrawCircle(Obj.Position, GetAutoAttackRange(Obj, Player), Config.Item("OW_Draw_AARangeEnemy").GetValue<Circle>().Color);
            }
            if (Config.Item("OW_Draw_HoldZone").GetValue<Circle>().Active) Utility.DrawCircle(Player.Position, Config.Item("OW_Misc_HoldZone").GetValue<Slider>().Value, Config.Item("OW_Draw_HoldZone").GetValue<Circle>().Color);
            if (Config.Item("OW_Draw_LastHit").GetValue<Circle>().Active || Config.Item("OW_Draw_NearKill").GetValue<Circle>().Active)
            {
                foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => Program.IsValid(i, GetAutoAttackRange(Player, i) + 500)))
                {
                    if (Config.Item("OW_Draw_LastHit").GetValue<Circle>().Active && Obj.Health <= Player.GetAutoAttackDamage(Obj, true))
                    {
                        Utility.DrawCircle(Obj.Position, Obj.BoundingRadius, Config.Item("OW_Draw_LastHit").GetValue<Circle>().Color);
                    }
                    else if (Config.Item("OW_Draw_NearKill").GetValue<Circle>().Active && Obj.Health <= Player.GetAutoAttackDamage(Obj, true) * 2) Utility.DrawCircle(Obj.Position, Obj.BoundingRadius, Config.Item("OW_Draw_NearKill").GetValue<Circle>().Color);
                }
            }
        }

        public static void ResetAutoAttack()
        {
            LastAttack = 0;
        }

        private static void OnGameProcessPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] != 0x34 || new GamePacket(args).ReadInteger(1) != Player.NetworkId || (args.PacketData[5] != 0x11 && args.PacketData[5] != 0x91)) return;
            ResetAutoAttack();
        }

        private static void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (IsAutoAttackReset(args.SData.Name) && sender.IsMe) Utility.DelayAction.Add(200, ResetAutoAttack);
            if (!IsAutoAttack(args.SData.Name)) return;
            if (sender.IsMe)
            {
                LastAttack = Environment.TickCount - Game.Ping / 2;
                if (args.Target != null && args.Target.IsValid)
                {
                    FireOnTargetSwitch((Obj_AI_Base)args.Target);
                    LastTarget = (Obj_AI_Base)args.Target;
                }
                if (sender.IsMelee()) Utility.DelayAction.Add((int)(sender.AttackCastDelay * 1000 + 40), () => FireAfterAttack(sender, LastTarget));
            }
            FireOnAttack(sender, LastTarget);
        }

        private static void OnCreateObjMissile(GameObject sender, EventArgs args)
        {
            if (sender.IsMe && (sender as Obj_AI_Hero).IsMelee()) return;
            if (!sender.IsValid<Obj_SpellMissile>()) return;
            var missile = (Obj_SpellMissile)sender;
            if (missile.SpellCaster.IsValid<Obj_AI_Hero>() && IsAutoAttack(missile.SData.Name))
            {
                FireAfterAttack(missile.SpellCaster, LastTarget);
                if (sender.IsMe) LastRealAttack = Environment.TickCount;
            }
        }

        private static readonly Random RandomPos = new Random(DateTime.Now.Millisecond);
        private static void MoveTo(Vector3 Pos)
        {
            if (Environment.TickCount - LastMove < Config.Item("OW_Misc_Humanizer").GetValue<Slider>().Value) return;
            LastMove = Environment.TickCount;
            if (Player.Distance(Pos) < Config.Item("OW_Misc_HoldZone").GetValue<Slider>().Value)
            {
                if (Player.Path.Count() > 1) Player.IssueOrder(GameObjectOrder.HoldPosition, Player.ServerPosition);
                return;
            }
            Player.IssueOrder(GameObjectOrder.MoveTo, Player.ServerPosition.To2D().Extend(Pos.To2D(), (RandomPos.NextFloat(0.6f, 1) + 0.2f) * 300).To3D());
            //Player.IssueOrder(GameObjectOrder.MoveTo, Player.Distance(Pos) > 300 ? Player.ServerPosition.To2D().Extend(Pos.To2D(), 300).To3D() : Pos);
        }

        public static void Orbwalk(Vector3 Pos, Obj_AI_Base Target)
        {
            if (Program.IsValid(Target) && (CanAttack() || HaveCancled()) && IsAllowedToAttack())
            {
                DisableNextAttack = false;
                FireBeforeAttack(Target);
                if (!DisableNextAttack)
                {
                    Player.IssueOrder(GameObjectOrder.AttackUnit, Target);
                    if (LastTarget != null && LastTarget.IsValid && LastTarget != Target) LastAttack = Environment.TickCount + Game.Ping / 2;
                    LastTarget = Target;
                    return;
                }
            }
            if (!CanMove() || !IsAllowedToMove()) return;
            if (Player.IsMelee() && Program.IsValid(Target) && InAutoAttackRange(Target) && Config.Item("OW_Misc_MeleePrediction").GetValue<bool>() && Target is Obj_AI_Hero && Game.CursorPos.Distance(Target.Position) < 300)
            {
                MovePrediction.Delay = Player.BasicAttack.SpellCastTime;
                MovePrediction.Speed = Player.BasicAttack.MissileSpeed;
                MoveTo(MovePrediction.GetPrediction(Target).UnitPosition);
            }
            else MoveTo(Pos);
        }

        private static bool IsAllowedToAttack()
        {
            if (!Attack || Config.Item("OW_Misc_AllAttackDisabled").GetValue<bool>()) return false;
            if (CurrentMode == Mode.Combo && !Config.Item("OW_Combo_Attack").GetValue<bool>()) return false;
            if (CurrentMode == Mode.Harass && !Config.Item("OW_Harass_Attack").GetValue<bool>()) return false;
            if (CurrentMode == Mode.LaneClear && !Config.Item("OW_Clear_Attack").GetValue<bool>()) return false;
            if (CurrentMode == Mode.LaneFreeze && !Config.Item("OW_Freeze_Attack").GetValue<bool>()) return false;
            return CurrentMode != Mode.LastHit || Config.Item("OW_LastHit_Attack").GetValue<bool>();
        }

        private static bool IsAllowedToMove()
        {
            if (!Move || Config.Item("OW_Misc_AllMovementDisabled").GetValue<bool>()) return false;
            if (CurrentMode == Mode.Combo && !Config.Item("OW_Combo_Move").GetValue<bool>()) return false;
            if (CurrentMode == Mode.Harass && !Config.Item("OW_Harass_Move").GetValue<bool>()) return false;
            if (CurrentMode == Mode.LaneClear && !Config.Item("OW_Clear_Move").GetValue<bool>()) return false;
            if (CurrentMode == Mode.LaneFreeze && !Config.Item("OW_Freeze_Move").GetValue<bool>()) return false;
            if (CurrentMode == Mode.LastHit && !Config.Item("OW_LastHit_Move").GetValue<bool>()) return false;
            return CurrentMode != Mode.LastHit || Config.Item("OW_LastHit_Move").GetValue<bool>();
        }

        private static void CheckAutoWindUp()
        {
            if (!Config.Item("OW_Misc_AutoWindUp").GetValue<bool>())
            {
                WindUp = GetCurrentWindupTime();
                return;
            }
            var additional = 0;
            if (Game.Ping >= 100)
            {
                additional = Game.Ping / 100 * 5;
            }
            else if (Game.Ping > 40 && Game.Ping < 100)
            {
                additional = Game.Ping / 100 * 10;
            }
            else if (Game.Ping <= 40) additional = 20;
            var windUp = Game.Ping + additional;
            if (windUp < 40) windUp = 40;
            Config.Item("OW_Misc_ExtraWindUp").SetValue(windUp < 200 ? new Slider(windUp, 200, 0) : new Slider(200, 200, 0));
            WindUp = windUp;
        }

        private static int GetCurrentWindupTime()
        {
            return Config.Item("OW_Misc_ExtraWindUp").GetValue<Slider>().Value;
        }

        public static bool IsAutoAttackReset(string Name)
        {
            return AttackResets.Contains(Name.ToLower());
        }

        public static bool IsAutoAttack(string Name)
        {
            return (Name.ToLower().Contains("attack") && !NoAttacks.Contains(Name.ToLower())) || Attacks.Contains(Name.ToLower());
        }

        public static float GetAutoAttackRange(Obj_AI_Base Source = null, Obj_AI_Base Target = null)
        {
            if (Source == null) Source = Player;
            var Result = Source.AttackRange + Source.BoundingRadius;
            if (Program.IsValid(Target)) Result += Target.BoundingRadius;
            return Result;
        }

        public static bool InAutoAttackRange(Obj_AI_Base Target)
        {
            if (!Program.IsValid(Target)) return false;
            return Player.Distance3D(Target) <= GetAutoAttackRange(Player, Target);
        }

        public static bool CanAttack()
        {
            if (LastAttack <= Environment.TickCount) return Environment.TickCount + Game.Ping / 2 + 25 >= LastAttack + Player.AttackDelay * 1000 && Attack;
            return false;
        }

        private static bool HaveCancled()
        {
            if (LastAttack - Environment.TickCount > Player.AttackCastDelay * 1000 + 25) return LastRealAttack < LastAttack;
            return false;
        }

        public static bool CanMove()
        {
            if (LastAttack <= Environment.TickCount) return Environment.TickCount + Game.Ping / 2 >= LastAttack + Player.AttackCastDelay * 1000 + WindUp && Move;
            return false;
        }

        public static Mode CurrentMode
        {
            get
            {
                if (Config.Item("OW_Combo_Key").GetValue<KeyBind>().Active) return Mode.Combo;
                if (Config.Item("OW_Harass_Key").GetValue<KeyBind>().Active) return Mode.Harass;
                if (Config.Item("OW_Clear_Key").GetValue<KeyBind>().Active) return Mode.LaneClear;
                if (Config.Item("OW_Freeze_Key").GetValue<KeyBind>().Active) return Mode.LaneFreeze;
                if (Config.Item("OW_LastHit_Key").GetValue<KeyBind>().Active) return Mode.LastHit;
                return Config.Item("OW_Flee_Key").GetValue<KeyBind>().Active ? Mode.Flee : Mode.None;
            }
        }

        public static void SetAttack(bool Value)
        {
            Attack = Value;
        }

        public static void SetMovement(bool Value)
        {
            Move = Value;
        }

        private static bool ShouldWait()
        {
            return ObjectManager.Get<Obj_AI_Minion>().Any(i => Program.IsValid(i) && i.Team != GameObjectTeam.Neutral && InAutoAttackRange(i) && HealthPrediction.LaneClearHealthPrediction(i, (int)(Player.AttackDelay * 1000 * ClearWaitTimeMod), Config.Item("OW_Misc_FarmDelay").GetValue<Slider>().Value) <= Player.GetAutoAttackDamage(i));
        }

        private static Obj_AI_Base GetPossibleTarget()
        {
            Obj_AI_Base Target = null;
            if (Config.Item("OW_Misc_PriorityUnit").GetValue<StringList>().SelectedIndex == 1 && (CurrentMode == Mode.Harass || CurrentMode == Mode.LaneClear || CurrentMode == Mode.LaneFreeze))
            {
                var Obj = GetBestHeroTarget();
                if (Program.IsValid(Obj)) return Obj;
            }
            if (CurrentMode == Mode.Harass || CurrentMode == Mode.LaneClear || CurrentMode == Mode.LaneFreeze || CurrentMode == Mode.LastHit)
            {
                foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => Program.IsValid(i) && i.Name != "Beacon" && InAutoAttackRange(i) && i.Team != GameObjectTeam.Neutral && i.Health < 2 * Player.GetAutoAttackDamage(i, true)))
                {
                    var Time = (int)(Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2 + 1000 * (int)(Player.Distance3D(Obj) / Orbwalking.GetMyProjectileSpeed());
                    var predHp = HealthPrediction.GetHealthPrediction(Obj, Time, Config.Item("OW_Misc_FarmDelay").GetValue<Slider>().Value);
                    if (predHp <= 0) FireOnNonKillableMinion(Obj);
                    if (predHp > 0 && predHp <= Player.GetAutoAttackDamage(Obj, true)) return Obj;
                }
            }
            if (Program.IsValid(ForcedTarget) && InAutoAttackRange(ForcedTarget)) return ForcedTarget;
            if (CurrentMode != Mode.LastHit)
            {
                var Obj = GetBestHeroTarget();
                if (Program.IsValid(Obj)) return Obj;
            }
            var maxHp = float.MaxValue;
            if (CurrentMode == Mode.Harass || CurrentMode == Mode.LaneClear || CurrentMode == Mode.LaneFreeze)
            {
                foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => Program.IsValid(i) && i.Name != "Beacon" && InAutoAttackRange(i) && i.Team == GameObjectTeam.Neutral && (i.MaxHealth >= maxHp || Math.Abs(maxHp - float.MaxValue) < float.Epsilon)))
                {
                    Target = Obj;
                    maxHp = Obj.MaxHealth;
                }
                if (Program.IsValid(Target)) return Target;
            }
            if ((CurrentMode == Mode.LaneClear || CurrentMode == Mode.LaneFreeze) && !ShouldWait())
            {
                if (Program.IsValid(PrevMinion) && InAutoAttackRange(PrevMinion))
                {
                    var predHp = HealthPrediction.LaneClearHealthPrediction(PrevMinion, (int)(Player.AttackDelay * 1000 * ClearWaitTimeMod), Config.Item("OW_Misc_FarmDelay").GetValue<Slider>().Value);
                    if (predHp >= Player.GetAutoAttackDamage(PrevMinion, true) * 2 || Math.Abs(predHp - PrevMinion.Health) < float.Epsilon) return PrevMinion;
                }
                foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => Program.IsValid(i) && i.Name != "Beacon" && InAutoAttackRange(i)))
                {
                    var predHp = HealthPrediction.LaneClearHealthPrediction(Obj, (int)(Player.AttackDelay * 1000 * ClearWaitTimeMod), Config.Item("OW_Misc_FarmDelay").GetValue<Slider>().Value);
                    if (predHp >= Player.GetAutoAttackDamage(Obj, true) * 2 || Math.Abs(predHp - Obj.Health) < float.Epsilon)
                    {
                        if (Obj.Health >= maxHp || Math.Abs(maxHp - float.MaxValue) < float.Epsilon)
                        {
                            Target = Obj;
                            maxHp = Obj.Health;
                            PrevMinion = Obj;
                        }
                    }
                }
                if (Program.IsValid(Target)) return Target;
            }
            if (CurrentMode == Mode.Harass || CurrentMode == Mode.LaneClear || CurrentMode == Mode.LaneFreeze)
            {
                foreach (var Obj in ObjectManager.Get<Obj_AI_Turret>().Where(i => Program.IsValid(i) && InAutoAttackRange(i))) return Obj;
            }
            return null;
        }

        private static Obj_AI_Hero GetBestHeroTarget()
        {
            Obj_AI_Hero KillableObj = null;
            var HitsToKill = double.MaxValue;
            foreach (var Obj in AllEnemys.Where(i => Program.IsValid(i) && InAutoAttackRange(i)))
            {
                var KillHits = Obj.Health / Player.GetAutoAttackDamage(Obj, true);
                if (Program.IsValid(KillableObj) && (!(KillHits < HitsToKill) || Obj.HasBuffOfType(BuffType.Invulnerability))) continue;
                KillableObj = Obj;
                HitsToKill = KillHits;
            }
            return HitsToKill <= 3 ? KillableObj : SimpleTs.GetTarget(GetAutoAttackRange(), SimpleTs.DamageType.Physical);
        }

        private static void FireBeforeAttack(Obj_AI_Base Target)
        {
            if (BeforeAttack != null)
            {
                BeforeAttack(new BeforeAttackEventArgs { Target = Target });
            }
            else DisableNextAttack = false;
        }

        private static void FireOnAttack(Obj_AI_Base Unit, Obj_AI_Base Target)
        {
            if (OnAttack != null) OnAttack(Unit, Target);
        }

        private static void FireAfterAttack(Obj_AI_Base Unit, Obj_AI_Base Target)
        {
            if (AfterAttack != null) AfterAttack(Unit, Target);
        }

        private static void FireOnTargetSwitch(Obj_AI_Base NewTarget)
        {
            if (OnTargetChange != null && (!Program.IsValid(LastTarget) || LastTarget != NewTarget)) OnTargetChange(LastTarget, NewTarget);
        }

        private static void FireOnNonKillableMinion(Obj_AI_Base Minion)
        {
            if (OnNonKillableMinion != null) OnNonKillableMinion(Minion);
        }
    }
}