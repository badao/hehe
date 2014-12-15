using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Warwick : Program
    {
        public Warwick()
        {
            Q = new Spell(SpellSlot.Q, 400);
            W = new Spell(SpellSlot.W, 1250);
            E = new Spell(SpellSlot.E, 1600);
            R = new Spell(SpellSlot.R, 700);
            Q.SetTargetted(-0.5f, 20);
            R.SetTargetted(-0.5f, 0);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemList(ComboMenu, "WMode", "-> Mode", new[] { "Always", "# Ally" });
                    ItemSlider(ComboMenu, "WAbove", "--> If Ally Above", 2, 1, 4);
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemBool(ComboMenu, "Smite", "Use Challenging Smite");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
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
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy)) ItemBool(UltiMenu, Obj.ChampionName, "Use R On " + Obj.ChampionName);
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemBool(MiscMenu, "RUnderTower", "Use R If Enemy Under Tower");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 5, 0, 7).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit) LastHit();
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
            if (ItemBool("Misc", "RUnderTower")) AutoRUnderTower();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "Q") && Q.IsReady() && Q.InRange(targetObj.Position)) Q.CastOnUnit(targetObj, PacketCast());
            if (ItemBool(Mode, "W") && W.IsReady() && ((Mode == "Harass" && Orbwalk.InAutoAttackRange(targetObj)) || (Mode == "Combo" && ((ItemList(Mode, "WMode") == 0 && Orbwalk.InAutoAttackRange(targetObj)) || (ItemList(Mode, "WMode") == 1 && ObjectManager.Get<Obj_AI_Hero>().Count(i => IsValid(i, W.Range, false)) >= ItemSlider(Mode, "WAbove")))))) W.Cast(PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
            if (Mode == "Combo" && ItemBool(Mode, "Smite") && SmiteReady() && Player.GetSpellSlot("s5_summonersmiteduel") != SpellSlot.Unknown) CastSmite(targetObj, false);
            if (Mode == "Combo" && ItemBool(Mode, "R") && ItemBool("Ultimate", targetObj.ChampionName) && R.IsReady() && R.InRange(targetObj.Position)) R.CastOnUnit(targetObj, PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, R.Range)).OrderBy(i => i.Health))
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "Q") && Q.IsReady() && Q.InRange(Obj.Position) && (CanKill(Obj, Q) || Obj.MaxHealth >= 1200 || Obj.HealthPercentage() > 50)) Q.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "W") && W.IsReady() && Orbwalk.InAutoAttackRange(Obj)) W.Cast(PacketCast());
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q)).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player))) Q.CastOnUnit(Obj, PacketCast());
        }

        private void KillSteal()
        {
            if (!Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) Q.CastOnUnit(Obj, PacketCast());
        }

        private void AutoRUnderTower()
        {
            if (Player.UnderTurret() || !R.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, R.Range)).OrderBy(i => i.Distance3D(Player)))
            {
                var TowerObj = ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(i => IsValid(i, 950, false));
                if (TowerObj != null && Obj.Distance3D(TowerObj) <= 950) R.CastOnUnit(Obj, PacketCast());
            }
        }

        private void UseItem(Obj_AI_Base Target)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Randuin);
        }
    }
}