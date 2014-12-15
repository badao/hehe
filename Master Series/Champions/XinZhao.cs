using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class XinZhao : Program
    {
        public XinZhao()
        {
            Q = new Spell(SpellSlot.Q, 375);
            W = new Spell(SpellSlot.W, 20);
            E = new Spell(SpellSlot.E, 650);
            R = new Spell(SpellSlot.R, 500);
            Q.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);
            E.SetTargetted(-0.5f, 20);
            R.SetSkillshot(-0.5f, 0, 347.8f, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R If Killable");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
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
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    var KillableMenu = new Menu("Killable", "Killable");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy)) ItemBool(KillableMenu, Obj.ChampionName, "Use R On " + Obj.ChampionName);
                        UltiMenu.AddSubMenu(KillableMenu);
                    }
                    var InterruptMenu = new Menu("Interrupt", "Interrupt");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
                        {
                            foreach (var Spell in Interrupter.Spells.Where(i => i.ChampionName == Obj.ChampionName)) ItemBool(InterruptMenu, Obj.ChampionName + "_" + Spell.Slot.ToString(), "Spell " + Spell.Slot.ToString() + " Of " + Obj.ChampionName);
                        }
                        UltiMenu.AddSubMenu(InterruptMenu);
                    }
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "EKillSteal", "Use E To Kill Steal");
                    ItemBool(MiscMenu, "RInterrupt", "Use R To Interrupt");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 5, 0, 5).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ItemBool(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) LaneJungClear();
            if (ItemBool("Misc", "EKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "RInterrupt") || !ItemBool("Interrupt", (unit as Obj_AI_Hero).ChampionName + "_" + spell.Slot.ToString()) || Player.IsDead || !R.IsReady()) return;
            if (!R.InRange(unit.Position) && E.IsReady() && Player.Mana >= E.Instance.ManaCost + R.Instance.ManaCost)
            {
                foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, E.Range) && !(i is Obj_AI_Turret) && i != unit && i.Distance3D(unit) <= R.Range - 30)) E.CastOnUnit(Obj, PacketCast());
            }
            if (IsValid(unit, R.Range) && !unit.HasBuff("XinZhaoIntimidate")) R.Cast(PacketCast());
        }

        private void AfterAttack(Obj_AI_Base Unit, Obj_AI_Base Target)
        {
            if (!Unit.IsMe) return;
            if ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass) && ItemBool(Orbwalk.CurrentMode.ToString(), "Q") && IsValid(Target, Orbwalk.GetAutoAttackRange() + 50) && Q.IsReady() && Target.HasBuff("XinZhaoIntimidate")) Q.Cast(PacketCast());
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (Mode == "Combo" && ItemBool(Mode, "R") && ItemBool("Killable", targetObj.ChampionName) && R.IsReady() && R.InRange(targetObj.Position))
            {
                if (CanKill(targetObj, R))
                {
                    R.Cast(PacketCast());
                }
                else if (R.GetHealthPrediction(targetObj) - R.GetDamage(targetObj) + 5 <= E.GetDamage(targetObj) + Player.GetAutoAttackDamage(targetObj, true) + ((ItemBool(Mode, "Q") && Q.IsReady()) ? Q.GetDamage(targetObj) * 3 : 0) && ItemBool(Mode, "E") && E.IsReady() && Player.Mana >= R.Instance.ManaCost + E.Instance.ManaCost + ((ItemBool(Mode, "Q") && Q.IsReady()) ? Q.Instance.ManaCost : 0)) R.Cast(PacketCast());
            }
            if (ItemBool(Mode, "E") && E.IsReady() && E.InRange(targetObj.Position) && (CanKill(targetObj, E) || Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange() + 50 || (Mode == "Combo" && Player.HealthPercentage() < targetObj.HealthPercentage()))) E.CastOnUnit(targetObj, PacketCast());
            if (ItemBool(Mode, "W") && W.IsReady() && Orbwalk.InAutoAttackRange(targetObj)) W.Cast(PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, E.Range)).OrderBy(i => i.Health))
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady() && (Player.Distance3D(Obj) > Orbwalk.GetAutoAttackRange() + 50 || CanKill(Obj, E) || Obj.MaxHealth >= 1200)) E.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "W") && W.IsReady() && Orbwalk.InAutoAttackRange(Obj)) W.Cast(PacketCast());
                if (ItemBool("Clear", "Q") && Q.IsReady() && Player.Distance3D(Obj) <= Orbwalk.GetAutoAttackRange() + 50) Q.Cast(PacketCast());
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void KillSteal()
        {
            if (!E.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, E.Range) && CanKill(i, E) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) E.CastOnUnit(Obj, PacketCast());
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
            if (Items.CanUseItem(Youmuu) && Player.CountEnemysInRange(350) >= 1 && !IsFarm) Items.UseItem(Youmuu);
        }
    }
}