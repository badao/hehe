using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Maokai : Program
    {
        public Maokai()
        {
            Q = new Spell(SpellSlot.Q, 600);
            W = new Spell(SpellSlot.W, 525);
            E = new Spell(SpellSlot.E, 1100);
            R = new Spell(SpellSlot.R, 500);
            Q.SetSkillshot(-0.2222f, 110, 1100, false, SkillshotType.SkillshotLine);
            W.SetTargetted(-0.5f, 1000);
            E.SetSkillshot(-0.25f, 0, 1750, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(-0.5f, 150, 20, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemSlider(ComboMenu, "RAbove", "-> Cancel R2 If Mp Under", 20);
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemSlider(HarassMenu, "WAbove", "-> If Hp Above", 20);
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
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemBool(MiscMenu, "QAntiGap", "Use Q To Anti Gap Closer");
                    ItemBool(MiscMenu, "QInterrupt", "Use Q To Interrupt");
                    ItemBool(MiscMenu, "WUnderTower", "Use W If Enemy Under Tower");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 5, 0, 5).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "W", "W Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ItemBool(DrawMenu, "R", "R Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) LaneJungClear();
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
            if (ItemBool("Misc", "WUnderTower")) AutoWUnderTower();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "QAntiGap") || Player.IsDead || !Q.IsReady()) return;
            if (IsValid(gapcloser.Sender, Q.Range) && Player.Distance3D(gapcloser.Sender) <= 100) Q.Cast(gapcloser.Sender.Position, PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "QInterrupt") || Player.IsDead || !Q.IsReady()) return;
            if (Player.Mana >= Q.Instance.ManaCost + W.Instance.ManaCost && Player.Distance3D(unit) > 100 && IsValid(unit, W.Range) && W.IsReady()) W.CastOnUnit(unit, PacketCast());
            if (IsValid(unit, Q.Range) && Player.Distance3D(unit) <= 100) Q.Cast(unit.Position, PacketCast());
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "E") && E.IsReady() && E.InRange(targetObj.Position)) E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 50), PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "R") && R.IsReady() && R.InRange(targetObj.Position))
            {
                if (Player.ManaPercentage() >= ItemSlider(Mode, "RAbove"))
                {
                    if (!Player.HasBuff("MaokaiDrain")) R.Cast(PacketCast());
                }
                else if (Player.HasBuff("MaokaiDrain")) R.Cast(PacketCast());
            }
            if (ItemBool(Mode, "W") && W.IsReady() && W.InRange(targetObj.Position) && (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= ItemSlider(Mode, "WAbove")))) W.CastOnUnit(targetObj, PacketCast());
            if (ItemBool(Mode, "Q") && Q.IsReady() && Q.InRange(targetObj.Position)) Q.Cast(targetObj.Position, PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item") && Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Randuin);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, E.Range) && i is Obj_AI_Minion).OrderBy(i => i.Health);
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady() && minionObj.Count() >= 2)
                {
                    var posEFarm = E.GetCircularFarmLocation(minionObj.ToList());
                    E.Cast(posEFarm.MinionsHit >= 2 ? posEFarm.Position : Player.Position.To2D().Extend(Obj.Position.To2D(), Obj.Distance3D(Player) + 50), PacketCast());
                }
                if (ItemBool("Clear", "W") && W.IsReady() && W.InRange(Obj.Position) && (W.GetHealthPrediction(Obj) + 5 <= (W.GetDamage(Obj) > 300 ? Player.CalcDamage(Obj, Damage.DamageType.Magical, 300) : W.GetDamage(Obj)) || Obj.Team == GameObjectTeam.Neutral)) W.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "Q") && Q.IsReady())
                {
                    var posQFarm = Q.GetLineFarmLocation(minionObj.Where(i => IsValid(i, Q.Range)).ToList());
                    Q.Cast(posQFarm.MinionsHit >= 2 ? posQFarm.Position : minionObj.First(i => IsValid(i, Q.Range)).Position.To2D(), PacketCast());
                }
            }
        }

        private void KillSteal()
        {
            if (!Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) Q.Cast(Obj.Position, PacketCast());
        }

        private void AutoWUnderTower()
        {
            if (Player.UnderTurret() || !W.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, W.Range)).OrderBy(i => i.Distance3D(Player)))
            {
                var TowerObj = ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(i => IsValid(i, 950, false));
                if (TowerObj != null && Obj.Distance3D(TowerObj) <= 950) W.CastOnUnit(Obj, PacketCast());
            }
        }
    }
}