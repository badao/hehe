using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace MasterPlugin
{
    class Amumu : Master.Program
    {
        public Amumu()
        {
            SkillQ = new Spell(SpellSlot.Q, 1100);
            SkillW = new Spell(SpellSlot.W, 300);
            SkillE = new Spell(SpellSlot.E, 350);
            SkillR = new Spell(SpellSlot.R, 550);
            SkillQ.SetSkillshot(-0.5f, 80, 2000, true, SkillshotType.SkillshotLine);
            SkillW.SetSkillshot(-0.3864f, 0, 0, false, SkillshotType.SkillshotCircle);
            SkillE.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);
            SkillR.SetSkillshot(-0.5f, 0, 20, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu(Name + " Plugin", Name + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemSlider(ComboMenu, "WAbove", "-> If Mp Above", 20);
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemList(ComboMenu, "RMode", "-> Mode", new[] { "Finish", "# Enemy" });
                    ItemSlider(ComboMenu, "RAbove", "-> If Enemy Above", 2, 1, 4);
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemSlider(HarassMenu, "WAbove", "-> If Mp Above", 20);
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
                    ItemSlider(ClearMenu, "WAbove", "-> If Mp Above", 20);
                    ItemBool(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QAntiGap", "Use Q To Anti Gap Closer");
                    ItemBool(MiscMenu, "SmiteCol", "Auto Smite Collision");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 6, 0, 7).ValueChanged += SkinChanger;
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
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo)
            {
                NormalCombo();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                Harass();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) LaneJungClear();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, SkillQ.Range, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && SkillW.Level > 0) Utility.DrawCircle(Player.Position, SkillW.Range, SkillW.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "QAntiGap")) return;
            if (IsValid(gapcloser.Sender, SkillQ.Range) && SkillQ.IsReady() && Player.Distance3D(gapcloser.Sender) < 400) SkillQ.Cast(gapcloser.Sender.Position, PacketCast());
        }

        private void NormalCombo()
        {
            if (ItemBool("Combo", "W") && SkillW.IsReady() && Player.HasBuff("AuraofDespair", true) && Player.CountEnemysInRange(500) == 0) SkillW.Cast(PacketCast());
            if (targetObj == null) return;
            if (ItemBool("Combo", "Q") && SkillQ.IsReady())
            {
                var nearObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, SkillQ.Range) && !(i is Obj_AI_Turret) && i.CountEnemysInRange((int)SkillR.Range - 40) >= ItemSlider("Combo", "RAbove") && !CanKill(i, SkillQ)).OrderBy(i => i.CountEnemysInRange((int)SkillR.Range));
                if (ItemBool("Combo", "R") && SkillR.IsReady() && ItemList("Combo", "RMode") == 1 && nearObj.Count() > 0)
                {
                    foreach (var Obj in nearObj) SkillQ.CastIfHitchanceEquals(Obj, HitChance.VeryHigh, PacketCast());
                }
                else if (SkillQ.InRange(targetObj.Position) && (CanKill(targetObj, SkillQ) || !Orbwalk.InAutoAttackRange(targetObj)))
                {
                    if (ItemBool("Misc", "SmiteCol"))
                    {
                        if (!SmiteCollision(targetObj, SkillQ)) SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                    }
                    else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                }
            }
            if (ItemBool("Combo", "W") && SkillW.IsReady())
            {
                if (Player.Mana * 100 / Player.MaxMana >= ItemSlider("Combo", "WAbove"))
                {
                    if (Player.Distance3D(targetObj) <= SkillW.Range + 35)
                    {
                        if (!Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
                }
                else if (Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
            }
            if (ItemBool("Combo", "E") && SkillE.IsReady() && SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast());
            if (ItemBool("Combo", "R") && SkillR.IsReady())
            {
                switch (ItemList("Combo", "RMode"))
                {
                    case 0:
                        if (SkillR.InRange(targetObj.Position) && CanKill(targetObj, SkillR)) SkillR.Cast(PacketCast());
                        break;
                    case 1:
                        var Obj = ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, SkillR.Range));
                        if (Obj.Count() > 0 && (Obj.Count() >= ItemSlider("Combo", "RAbove") || (Obj.Count() >= 2 && Obj.Count(i => CanKill(i, SkillR)) >= 1))) SkillR.Cast(PacketCast());
                        break;
                }
            }
            if (ItemBool("Combo", "Item") && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (ItemBool("Combo", "Ignite")) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (ItemBool("Harass", "W") && SkillW.IsReady() && Player.HasBuff("AuraofDespair", true) && Player.CountEnemysInRange(500) == 0) SkillW.Cast(PacketCast());
            if (targetObj == null) return;
            if (ItemBool("Harass", "E") && SkillE.IsReady() && SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast());
            if (ItemBool("Harass", "W") && SkillW.IsReady())
            {
                if (Player.Mana * 100 / Player.MaxMana >= ItemSlider("Harass", "WAbove"))
                {
                    if (Player.Distance3D(targetObj) <= SkillW.Range + 35)
                    {
                        if (!Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
                }
                else if (Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
            }
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, SkillQ.Range)).OrderBy(i => i.Health);
            if (minionObj.Count() == 0 && ItemBool("Clear", "W") && SkillW.IsReady() && Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && SkillE.IsReady() && SkillE.InRange(Obj.Position)) SkillE.Cast(PacketCast());
                if (ItemBool("Clear", "W") && SkillW.IsReady())
                {
                    if (Player.Mana * 100 / Player.MaxMana >= ItemSlider("Clear", "WAbove"))
                    {
                        if (minionObj.Count(i => Player.Distance3D(i) <= SkillW.Range + 35) >= 2 || (Obj.MaxHealth >= 1200 && Player.Distance3D(Obj) <= SkillW.Range + 35))
                        {
                            if (!Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
                        }
                        else if (Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("AuraofDespair", true)) SkillW.Cast(PacketCast());
                }
                if (ItemBool("Clear", "Q") && SkillQ.IsReady() && (!Orbwalk.InAutoAttackRange(Obj) || CanKill(Obj, SkillQ))) SkillQ.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast());
            }
        }
    }
}