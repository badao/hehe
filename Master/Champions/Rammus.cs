using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace MasterPlugin
{
    class Rammus : Master.Program
    {
        public Rammus()
        {
            SkillQ = new Spell(SpellSlot.Q, 300);
            SkillW = new Spell(SpellSlot.W, 300);
            SkillE = new Spell(SpellSlot.E, 325);
            SkillR = new Spell(SpellSlot.R, 375);
            SkillQ.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);
            SkillE.SetTargetted(-0.5f, 0);
            SkillR.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);

            var ChampMenu = new Menu(Name + " Plugin", Name + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemList(ComboMenu, "EMode", "-> Mode", new[] { "Always", "W Ready" });
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemList(ComboMenu, "RMode", "-> Mode", new[] { "Always", "# Enemy" });
                    ItemSlider(ComboMenu, "RCount", "--> If Enemy Above", 2, 1, 4);
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemList(HarassMenu, "EMode", "-> Mode", new[] { "Always", "W Ready" });
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
                    ItemList(ClearMenu, "EMode", "-> Mode", new[] { "Always", "W Ready" });
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QAntiGap", "Use Q To Anti Gap Closer");
                    ItemBool(MiscMenu, "EInterrupt", "Use E To Interrupt");
                    ItemBool(MiscMenu, "WSurvive", "Try Use W To Survive");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 6, 0, 6).ValueChanged += SkinChanger;
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
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee && SkillQ.IsReady() && !Player.HasBuff("PowerBall")) SkillQ.Cast(PacketCast());
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "E") && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "QAntiGap") || Player.IsDead) return;
            if (IsValid(gapcloser.Sender, Orbwalk.GetAutoAttackRange() + 100) && SkillQ.IsReady() && !Player.HasBuff("PowerBall")) SkillQ.Cast(PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "EInterrupt") || Player.IsDead) return;
            if (IsValid(unit, SkillE.Range) && SkillE.IsReady()) SkillE.CastOnUnit(unit, PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsEnemy && ItemBool("Misc", "WSurvive") && SkillW.IsReady())
            {
                if (args.Target.IsMe && (Orbwalk.IsAutoAttack(args.SData.Name) && Player.Health <= sender.GetAutoAttackDamage(Player, true)))
                {
                    SkillW.Cast(PacketCast());
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                        {
                            if (Player.Health <= (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i)) SkillW.Cast(PacketCast());
                        }
                    }
                }
            }
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "Q") && SkillQ.IsReady() && Player.Distance3D(targetObj) <= ((Mode == "Combo") ? 800 : Orbwalk.GetAutoAttackRange() + 50) && !Player.HasBuff("PowerBall"))
            {
                if ((ItemBool(Mode, "E") && SkillE.IsReady() && !SkillE.InRange(targetObj.Position)) || !Player.HasBuff("DefensiveBallCurl")) SkillQ.Cast(PacketCast());
            }
            if (ItemBool(Mode, "W") && SkillW.IsReady() && Orbwalk.InAutoAttackRange(targetObj) && !Player.HasBuff("PowerBall")) SkillW.Cast(PacketCast());
            if (ItemBool(Mode, "E") && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && !Player.HasBuff("PowerBall"))
            {
                switch (ItemList(Mode, "EMode"))
                {
                    case 0:
                        SkillE.CastOnUnit(targetObj, PacketCast());
                        break;
                    case 1:
                        if (Player.HasBuff("DefensiveBallCurl")) SkillE.CastOnUnit(targetObj, PacketCast());
                        break;
                }
            }
            if (ItemBool(Mode, "R") && Mode == "Combo" && SkillR.IsReady())
            {
                switch (ItemList(Mode, "RMode"))
                {
                    case 0:
                        if (SkillR.InRange(targetObj.Position)) SkillR.Cast(PacketCast());
                        break;
                    case 1:
                        if (Player.CountEnemysInRange((int)SkillR.Range) >= ItemSlider(Mode, "RCount")) SkillR.Cast(PacketCast());
                        break;
                }
            }
            if (ItemBool(Mode, "Item") && Mode == "Combo" && Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Randuin);
            if (ItemBool(Mode, "Ignite") && Mode == "Combo") CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, 800)).OrderBy(i => i.Health))
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "Q") && SkillQ.IsReady() && !Player.HasBuff("PowerBall"))
                {
                    if ((ItemBool("Clear", "E") && SkillE.IsReady() && !SkillE.InRange(Obj.Position)) || !Player.HasBuff("DefensiveBallCurl")) SkillQ.Cast(PacketCast());
                }
                if (ItemBool("Clear", "W") && SkillW.IsReady() && Orbwalk.InAutoAttackRange(Obj) && !Player.HasBuff("PowerBall")) SkillW.Cast(PacketCast());
                if (ItemBool("Clear", "E") && SkillE.IsReady() && SkillE.InRange(Obj.Position) && !Player.HasBuff("PowerBall") && Obj.Team == GameObjectTeam.Neutral)
                {
                    switch (ItemList("Clear", "EMode"))
                    {
                        case 0:
                            SkillE.CastOnUnit(Obj, PacketCast());
                            break;
                        case 1:
                            if (Player.HasBuff("DefensiveBallCurl")) SkillE.CastOnUnit(Obj, PacketCast());
                            break;
                    }
                }
            }
        }
    }
}