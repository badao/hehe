using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Tryndamere : Program
    {
        public Tryndamere()
        {
            Q = new Spell(SpellSlot.Q, 318.9f);
            W = new Spell(SpellSlot.W, 850);
            E = new Spell(SpellSlot.E, 650);
            R = new Spell(SpellSlot.R, 400);
            W.SetSkillshot(-0.5f, 0, 500, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(0, 160, 700, false, SkillshotType.SkillshotLine);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemSlider(ComboMenu, "QUnder", "-> If Hp Under", 40);
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemSlider(HarassMenu, "EAbove", "-> If Hp Above", 20);
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
                    ItemSlider(ClearMenu, "QUnder", "-> If Hp Under", 40);
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "EKillSteal", "Use E To Kill Steal");
                    ItemBool(MiscMenu, "QSurvive", "Try Use Q To Survive");
                    ItemBool(MiscMenu, "RSurvive", "Try Use R To Survive");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 4, 0, 6).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "W", "W Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee && E.IsReady()) E.Cast(Game.CursorPos, PacketCast());
            if (ItemBool("Misc", "EKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsEnemy)
            {
                if ((ItemBool("Misc", "QSurvive") && Q.IsReady()) || (ItemBool("Misc", "RSurvive") && R.IsReady()))
                {
                    if (args.Target.IsMe && ((Orbwalk.IsAutoAttack(args.SData.Name) && Player.Health <= sender.GetAutoAttackDamage(Player, true)) || (args.SData.Name == "summonerdot" && Player.Health <= (sender as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite))))
                    {
                        if (ItemBool("Misc", "QSurvive") && Q.IsReady())
                        {
                            Q.Cast(PacketCast());
                            return;
                        }
                        if (ItemBool("Misc", "RSurvive") && R.IsReady())
                        {
                            R.Cast(PacketCast());
                            return;
                        }
                    }
                    else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                    {
                        for (var i = 3; i > -1; i--)
                        {
                            if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                            {
                                if (Player.Health <= (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i))
                                {
                                    if (ItemBool("Misc", "QSurvive") && Q.IsReady())
                                    {
                                        Q.Cast(PacketCast());
                                        return;
                                    }
                                    if (ItemBool("Misc", "RSurvive") && R.IsReady())
                                    {
                                        R.Cast(PacketCast());
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (Mode == "Combo" && ItemBool(Mode, "Q") && Q.IsReady() && Player.HealthPercentage() <= ItemSlider(Mode, "QUnder") && Player.CountEnemysInRange(800) >= 1) Q.Cast(PacketCast());
            if (ItemBool(Mode, "W") && W.IsReady() && W.InRange(targetObj.Position))
            {
                if (targetObj.IsFacing(Player) && (Player.GetAutoAttackDamage(targetObj, true) < targetObj.GetAutoAttackDamage(Player, true) || Player.Health < targetObj.Health))
                {
                    W.Cast(PacketCast());
                }
                else if (!targetObj.IsFacing(Player) && Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange() + 50) W.Cast(PacketCast());
            }
            if (ItemBool(Mode, "E") && E.IsReady() && E.InRange(targetObj.Position) && (CanKill(targetObj, E) || (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange() + 50 && (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= ItemSlider(Mode, "EAbove")))))) E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 200), PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
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
                if (ItemBool("Clear", "Q") && Q.IsReady() && Player.HealthPercentage() <= ItemSlider("Clear", "QUnder") && (minionObj.Count(i => Orbwalk.InAutoAttackRange(i)) >= 2 || (Obj.MaxHealth >= 1200 && Orbwalk.InAutoAttackRange(Obj)))) Q.Cast(PacketCast());
                if (ItemBool("Clear", "E") && E.IsReady())
                {
                    var posEFarm = E.GetLineFarmLocation(minionObj.ToList());
                    E.Cast(posEFarm.MinionsHit >= 2 ? posEFarm.Position : Obj.Position.To2D(), PacketCast());
                }
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void KillSteal()
        {
            if (!E.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, E.Range) && CanKill(i, E) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) E.Cast(Player.Position.To2D().Extend(Obj.Position.To2D(), Obj.Distance3D(Player) + 200), PacketCast());
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