using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Jax : Program
    {
        private int Sheen = 3057, Trinity = 3078;
        private bool WardCasted = false, ECasted = false;
        private int RCount = 0;
        private Vector3 WardPlacePos = default(Vector3);

        public Jax()
        {
            Q = new Spell(SpellSlot.Q, 700);
            W = new Spell(SpellSlot.W, 300);
            E = new Spell(SpellSlot.E, 300);
            R = new Spell(SpellSlot.R, 100);
            Q.SetTargetted(-0.5f, 0);
            W.SetSkillshot(0.0435f, 0, 0, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(0, 0, 1450, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemList(ComboMenu, "WMode", "-> Mode", new[] { "After AA", "After R" });
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemList(ComboMenu, "RMode", "-> Mode", new[] { "Player Hp", "# Enemy" });
                    ItemSlider(ComboMenu, "RUnder", "--> If Hp Under", 40);
                    ItemSlider(ComboMenu, "RCount", "--> If Enemy Above", 2, 1, 4);
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemSlider(HarassMenu, "QAbove", "-> If Hp Above", 20);
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    var SmiteMob = new Menu("Smite Mob If Killable", "SmiteMob");
                    {
                        ItemBool(SmiteMob, "Baron", "Baron Nashor");
                        ItemBool(SmiteMob, "Dragon", "Dragon");
                        ItemBool(SmiteMob, "Red", "Red Brambleback");
                        ItemBool(SmiteMob, "Blue", "Blue Sentinel");
                        ItemBool(SmiteMob, "Krug", "Ancient Krug");
                        ItemBool(SmiteMob, "Gromp", "Gromp");
                        ItemBool(SmiteMob, "Raptor", "Crimson Raptor");
                        ItemBool(SmiteMob, "Wolf", "Greater Murk Wolf");
                        ClearMenu.AddSubMenu(SmiteMob);
                    }
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ItemList(ClearMenu, "WMode", "-> Mode", new[] { "After AA", "After R" });
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "WJPink", "Ward Jump Use Pink Ward", false);
                    ItemBool(MiscMenu, "WLastHit", "Use W To Last Hit");
                    ItemBool(MiscMenu, "WQKillSteal", "Use WQ To Kill Steal");
                    ItemBool(MiscMenu, "EAntiGap", "Use E To Anti Gap Closer");
                    ItemBool(MiscMenu, "EInterrupt", "Use E To Interrupt");
                    ItemBool(MiscMenu, "RSurvive", "Try Use R To Survive");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 8, 0, 8).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Obj_AI_Minion.OnCreate += OnCreateObjMinion;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling())
            {
                if (Player.IsDead) RCount = 0;
                return;
            }
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalk.Mode.Combo:
                    NormalCombo();
                    break;
                case Orbwalk.Mode.Harass:
                    Harass();
                    break;
                case Orbwalk.Mode.LaneClear:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.LaneFreeze:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.LastHit:
                    LastHit();
                    break;
                case Orbwalk.Mode.Flee:
                    WardJump(Game.CursorPos);
                    break;
            }
            if (ItemBool("Misc", "WQKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "EAntiGap") || Player.IsDead || !E.IsReady()) return;
            if (IsValid(gapcloser.Sender, E.Range + 10)) E.Cast(PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "EInterrupt") || Player.IsDead || !E.IsReady()) return;
            if (Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost && !E.InRange(unit.Position) && IsValid(unit, Q.Range) && Q.IsReady()) Q.CastOnUnit(unit, PacketCast());
            if (IsValid(unit, E.Range)) E.Cast(PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsMe)
            {
                if (Orbwalk.IsAutoAttack(args.SData.Name) && IsValid((Obj_AI_Base)args.Target) && W.IsReady() && Orbwalk.CurrentMode == Orbwalk.Mode.LastHit && ItemBool("Misc", "WLastHit") && W.GetHealthPrediction((Obj_AI_Base)args.Target) + 5 <= GetBonusDmg((Obj_AI_Base)args.Target) && args.Target is Obj_AI_Minion) W.Cast(PacketCast());
                if (args.SData.Name == "JaxCounterStrike")
                {
                    ECasted = true;
                    Utility.DelayAction.Add(1800, () => ECasted = false);
                }
                if (args.SData.Name == "jaxrelentlessattack")
                {
                    RCount = 0;
                    if (W.IsReady() && IsValid((Obj_AI_Base)args.Target, Orbwalk.GetAutoAttackRange() + 50))
                    {
                        switch (Orbwalk.CurrentMode)
                        {
                            case Orbwalk.Mode.Combo:
                                if (ItemBool("Combo", "W") && ItemList("Combo", "WMode") == 1) W.Cast(PacketCast());
                                break;
                            case Orbwalk.Mode.LaneClear:
                                if (ItemBool("Clear", "W") && ItemList("Clear", "WMode") == 1) W.Cast(PacketCast());
                                break;
                            case Orbwalk.Mode.LaneFreeze:
                                if (ItemBool("Clear", "W") && ItemList("Clear", "WMode") == 1) W.Cast(PacketCast());
                                break;
                        }
                    }
                }
            }
            else if (sender.IsEnemy && ItemBool("Misc", "RSurvive") && R.IsReady())
            {
                if (args.Target.IsMe && (Orbwalk.IsAutoAttack(args.SData.Name) && Player.Health <= sender.GetAutoAttackDamage(Player, true)))
                {
                    R.Cast(PacketCast());
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                        {
                            if (Player.Health <= (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i)) R.Cast(PacketCast());
                        }
                    }
                }
            }
        }

        private void OnCreateObjMinion(GameObject sender, EventArgs args)
        {
            if (!sender.IsValid<Obj_AI_Minion>() || sender.IsEnemy || Player.IsDead || !Q.IsReady() || !WardCasted) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee && Player.Distance3D((Obj_AI_Minion)sender) <= Q.Range + sender.BoundingRadius && sender.Name.EndsWith("Ward"))
            {
                Q.CastOnUnit((Obj_AI_Minion)sender, PacketCast());
                return;
            }
        }

        private void AfterAttack(Obj_AI_Base Unit, Obj_AI_Base Target)
        {
            if (!Unit.IsMe) return;
            RCount += 1;
            if (W.IsReady() && IsValid(Target, Orbwalk.GetAutoAttackRange() + 50))
            {
                switch (Orbwalk.CurrentMode)
                {
                    case Orbwalk.Mode.Combo:
                        if (ItemBool("Combo", "W") && ItemList("Combo", "WMode") == 0) W.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.Harass:
                        if (ItemBool("Harass", "W") && (!ItemBool("Harass", "Q") || (ItemBool("Harass", "Q") && !Q.IsReady()))) W.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.LaneClear:
                        if (ItemBool("Clear", "W") && ItemList("Clear", "WMode") == 0) W.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.LaneFreeze:
                        if (ItemBool("Clear", "W") && ItemList("Clear", "WMode") == 0) W.Cast(PacketCast());
                        break;
                }
            }
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (ItemBool("Combo", "E") && E.IsReady())
            {
                if (!Player.HasBuff("JaxEvasion"))
                {
                    if ((ItemBool("Combo", "Q") && Q.IsReady() && Q.InRange(targetObj.Position)) || E.InRange(targetObj.Position)) E.Cast(PacketCast());
                }
                else if (E.InRange(targetObj.Position) && !IsValid(targetObj, E.Range - 3.5f)) E.Cast(PacketCast());
            }
            if (ItemBool("Combo", "Q") && Q.IsReady() && Q.InRange(targetObj.Position))
            {
                if ((ItemBool("Combo", "E") && E.IsReady() && Player.HasBuff("JaxEvasion") && !E.InRange(targetObj.Position)) || (!Orbwalk.InAutoAttackRange(targetObj) && Player.Distance3D(targetObj) > 450)) Q.CastOnUnit(targetObj, PacketCast());
            }
            if (ItemBool("Combo", "R") && R.IsReady())
            {
                switch (ItemList("Combo", "RMode"))
                {
                    case 0:
                        if (Player.HealthPercentage() <= ItemSlider("Combo", "RUnder")) R.Cast(PacketCast());
                        break;
                    case 1:
                        if (Player.CountEnemysInRange((int)Q.Range) >= ItemSlider("Combo", "RCount")) R.Cast(PacketCast());
                        break;
                }
            }
            if (ItemBool("Combo", "Item")) UseItem(targetObj);
            if (ItemBool("Combo", "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (targetObj == null) return;
            if (ItemBool("Harass", "W") && W.IsReady() && ItemBool("Harass", "Q") && Q.IsReady() && Q.InRange(targetObj.Position)) W.Cast(PacketCast());
            if (ItemBool("Harass", "E") && E.IsReady())
            {
                if (!Player.HasBuff("JaxEvasion"))
                {
                    if ((ItemBool("Harass", "Q") && Q.IsReady() && Q.InRange(targetObj.Position)) || E.InRange(targetObj.Position)) E.Cast(PacketCast());
                }
                else if (E.InRange(targetObj.Position) && !IsValid(targetObj, E.Range - 3.5f)) E.Cast(PacketCast());
            }
            if (ItemBool("Harass", "Q") && Q.IsReady() && Q.InRange(targetObj.Position) && Player.HealthPercentage() >= ItemList("Harass", "QAbove"))
            {
                if ((ItemBool("Harass", "E") && E.IsReady() && Player.HasBuff("JaxEvasion") && !E.InRange(targetObj.Position)) || (!Orbwalk.InAutoAttackRange(targetObj) && Player.Distance3D(targetObj) > 450)) Q.CastOnUnit(targetObj, PacketCast());
            }
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range)).OrderBy(i => i.Health))
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady())
                {
                    if (!Player.HasBuff("JaxEvasion"))
                    {
                        if ((ItemBool("Clear", "Q") && Q.IsReady()) || E.InRange(Obj.Position)) E.Cast(PacketCast());
                    }
                    else if (E.InRange(Obj.Position) && !ECasted) E.Cast(PacketCast());
                }
                if (ItemBool("Clear", "Q") && Q.IsReady() && ((ItemBool("Clear", "E") && E.IsReady() && Player.HasBuff("JaxEvasion") && !E.InRange(Obj.Position)) || (!Orbwalk.InAutoAttackRange(Obj) && Player.Distance3D(Obj) > 450))) Q.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "WLastHit") || !W.IsReady() || !Player.HasBuff("JaxEmpowerTwo")) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Orbwalk.GetAutoAttackRange() + 50) && W.GetHealthPrediction(i) + 5 <= GetBonusDmg(i)).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player)))
            {
                Orbwalk.SetAttack(false);
                Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                Orbwalk.SetAttack(true);
                break;
            }
        }

        private void WardJump(Vector3 Pos)
        {
            if (!Q.IsReady()) return;
            bool Casted = false;
            var JumpPos = Pos;
            if (GetWardSlot() != null && !WardCasted && Player.Position.Distance(JumpPos) > GetWardRange()) JumpPos = Player.Position.To2D().Extend(JumpPos.To2D(), GetWardRange()).To3D();
            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => (IsValid(i, Q.Range + i.BoundingRadius) || IsValid(i, Q.Range + i.BoundingRadius, false)) && !(i is Obj_AI_Turret) && i.Position.Distance(WardCasted ? WardPlacePos : JumpPos) < 230).OrderBy(i => i.Position.Distance(WardCasted ? WardPlacePos : JumpPos)))
            {
                Q.CastOnUnit(Obj, PacketCast());
                Casted = true;
                return;
            }
            if (!Casted && GetWardSlot() != null && !WardCasted)
            {
                GetWardSlot().UseItem(JumpPos);
                WardPlacePos = JumpPos;
                Utility.DelayAction.Add(800, () => WardPlacePos = default(Vector3));
                WardCasted = true;
                Utility.DelayAction.Add(800, () => WardCasted = false);
            }
        }

        private void KillSteal()
        {
            if (Player.Mana < Q.Instance.ManaCost + W.Instance.ManaCost) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, Q.Range) && Q.GetHealthPrediction(i) + 5 <= Q.GetDamage(i) + GetBonusDmg(i) && i != targetObj).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player)))
            {
                if (W.IsReady()) W.Cast(PacketCast());
                if (Q.IsReady() && Player.HasBuff("JaxEmpowerTwo")) Q.CastOnUnit(Obj, PacketCast());
            }
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
        }

        private double GetBonusDmg(Obj_AI_Base Target)
        {
            double DmgItem = 0;
            if (Items.HasItem(Sheen) && ((Items.CanUseItem(Sheen) && W.IsReady()) || Player.HasBuff("Sheen")) && Player.BaseAttackDamage > DmgItem) DmgItem = Player.BaseAttackDamage;
            if (Items.HasItem(Trinity) && ((Items.CanUseItem(Trinity) && W.IsReady()) || Player.HasBuff("Sheen")) && Player.BaseAttackDamage * 2 > DmgItem) DmgItem = Player.BaseAttackDamage * 2;
            return (W.IsReady() || Player.HasBuff("JaxEmpowerTwo") ? W.GetDamage(Target) : 0) + (RCount >= 2 ? R.GetDamage(Target) : 0) + Player.GetAutoAttackDamage(Target, true) + Player.CalcDamage(Target, Damage.DamageType.Physical, DmgItem);
        }
    }
}