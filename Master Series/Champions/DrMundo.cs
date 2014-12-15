using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class DrMundo : Program
    {
        public DrMundo()
        {
            Q = new Spell(SpellSlot.Q, 975);
            W = new Spell(SpellSlot.W, 325);
            E = new Spell(SpellSlot.E, 300);
            R = new Spell(SpellSlot.R, 20);
            Q.SetSkillshot(-0.5f, 75, 1500, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(-0.3864f, 0, 20, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemSlider(ComboMenu, "WAbove", "-> If Hp Above", 20);
                    ItemBool(ComboMenu, "E", "Use E");
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
                    ItemSlider(ClearMenu, "WAbove", "-> If Hp Above", 20);
                    ItemBool(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    ItemBool(UltiMenu, "RSurvive", "Try Use R To Survive");
                    ItemSlider(UltiMenu, "RUnder", "-> If Hp Under", 35);
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemBool(MiscMenu, "SmiteCol", "Auto Smite Collision");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 7, 0, 7).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "W", "W Range", false);
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit) LastHit();
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsEnemy && ItemBool("Ultimate", "RSurvive") && R.IsReady())
            {
                if (args.Target.IsMe && ((Orbwalk.IsAutoAttack(args.SData.Name) && (Player.Health - sender.GetAutoAttackDamage(Player, true)) * 100 / Player.MaxHealth <= ItemSlider("Ultimate", "RUnder")) || (args.SData.Name == "summonerdot" && (Player.Health - (sender as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) * 100 / Player.MaxHealth <= ItemSlider("Ultimate", "RUnder"))))
                {
                    R.Cast(PacketCast());
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                        {
                            if ((Player.Health - (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i)) * 100 / Player.MaxHealth <= ItemSlider("Ultimate", "RUnder")) R.Cast(PacketCast());
                        }
                    }
                }
            }
        }

        private void NormalCombo(string Mode)
        {
            if (ItemBool(Mode, "W") && W.IsReady() && Player.HasBuff("BurningAgony") && Player.CountEnemysInRange(500) == 0) W.Cast(PacketCast());
            if (targetObj == null) return;
            if (ItemBool(Mode, "W") && W.IsReady())
            {
                if (Player.HealthPercentage() >= ItemSlider(Mode, "WAbove"))
                {
                    if (Player.Distance3D(targetObj) <= W.Range + 60)
                    {
                        if (!Player.HasBuff("BurningAgony")) W.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("BurningAgony")) W.Cast(PacketCast());
                }
                else if (Player.HasBuff("BurningAgony")) W.Cast(PacketCast());
            }
            if (ItemBool(Mode, "Q") && Q.IsReady() && Q.InRange(targetObj.Position))
            {
                if (ItemBool("Misc", "SmiteCol"))
                {
                    if (!SmiteCollision(targetObj, Q)) Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                }
                else Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
            }
            if (ItemBool(Mode, "E") && E.IsReady() && Orbwalk.InAutoAttackRange(targetObj)) E.Cast(PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item") && Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Randuin);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range)).OrderBy(i => i.Health);
            if (minionObj.Count() == 0 && ItemBool("Clear", "W") && W.IsReady() && Player.HasBuff("BurningAgony")) W.Cast(PacketCast());
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady() && Orbwalk.InAutoAttackRange(Obj)) E.Cast(PacketCast());
                if (ItemBool("Clear", "W") && W.IsReady())
                {
                    if (Player.HealthPercentage() >= ItemSlider("Clear", "WAbove"))
                    {
                        if (minionObj.Count(i => Player.Distance3D(i) <= W.Range + 60) >= 2 || (Obj.MaxHealth >= 1200 && Player.Distance3D(Obj) <= W.Range + 60))
                        {
                            if (!Player.HasBuff("BurningAgony")) W.Cast(PacketCast());
                        }
                        else if (Player.HasBuff("BurningAgony")) W.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("BurningAgony")) W.Cast(PacketCast());
                }
                if (ItemBool("Clear", "Q") && Q.IsReady() && (Obj.MaxHealth >= 1200 || CanKill(Obj, Q))) Q.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast());
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q)).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player))) Q.CastIfHitchanceEquals(Obj, HitChance.VeryHigh, PacketCast());
        }

        private void KillSteal()
        {
            if (!Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player)))
            {
                if (ItemBool("Misc", "SmiteCol"))
                {
                    if (!SmiteCollision(Obj, Q)) Q.CastIfHitchanceEquals(Obj, HitChance.VeryHigh, PacketCast());
                }
                else Q.CastIfHitchanceEquals(Obj, HitChance.VeryHigh, PacketCast());
            }
        }
    }
}