using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace MasterPlugin
{
    class Garen : Master.Program
    {
        public Garen()
        {
            SkillQ = new Spell(SpellSlot.Q, 300);
            SkillW = new Spell(SpellSlot.W, 20);
            SkillE = new Spell(SpellSlot.E, 325);
            SkillR = new Spell(SpellSlot.R, 400);
            SkillQ.SetSkillshot(0.0435f, 0, 0, false, SkillshotType.SkillshotCircle);
            SkillE.SetSkillshot(0, 160, 700, false, SkillshotType.SkillshotCircle);
            SkillR.SetTargetted(-0.13f, 900);

            var ChampMenu = new Menu(Name + " Plugin", Name + "_Plugin");
            {
                var ComboMenu = new Menu("Combo/Harass", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemSlider(ComboMenu, "WUnder", "-> If Hp Under", 60);
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R If Killable");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "E", "Use E");
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
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo)
            {
                NormalCombo();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(true);
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit)
            {
                LastHit();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee && SkillQ.IsReady()) SkillQ.Cast(PacketCast());
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "E") && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsMe && Orbwalk.IsAutoAttack(args.SData.Name) && IsValid((Obj_AI_Base)args.Target) && CanKill((Obj_AI_Base)args.Target, SkillQ) && SkillQ.IsReady() && (args.Target is Obj_AI_Hero || args.Target is Obj_AI_Minion))
            {
                if (Orbwalk.CurrentMode != Orbwalk.Mode.None && Orbwalk.CurrentMode != Orbwalk.Mode.Flee) SkillQ.Cast(PacketCast());
            }
            else if (sender.IsEnemy && ItemBool("Misc", "WSurvive") && SkillW.IsReady())
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

        private void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (!unit.IsMe) return;
            if (ItemBool("Combo", "Q") && SkillQ.IsReady() && IsValid(target, Orbwalk.GetAutoAttackRange() + 50) && (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)) SkillQ.Cast(PacketCast());
        }

        private void NormalCombo(bool IsHarass = false)
        {
            if (targetObj == null) return;
            if (ItemBool("Combo", "Q") && SkillQ.IsReady() && Player.Distance3D(targetObj) <= (IsHarass ? Orbwalk.GetAutoAttackRange() + 50 : 800) && !Orbwalk.InAutoAttackRange(targetObj))
            {
                if (IsHarass)
                {
                    Orbwalk.SetAttack(false);
                    Player.IssueOrder(GameObjectOrder.AttackUnit, targetObj);
                    Orbwalk.SetAttack(true);
                }
                else SkillQ.Cast(PacketCast());
            }
            if (ItemBool("Combo", "E") && SkillE.IsReady() && !Player.HasBuff("GarenE") && !Player.HasBuff("GarenQBuff") && SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast());
            if (ItemBool("Combo", "W") && SkillW.IsReady() && Orbwalk.InAutoAttackRange(targetObj) && Player.HealthPercentage() <= ItemSlider("Combo", "WUnder")) SkillW.Cast(PacketCast());
            if (ItemBool("Combo", "R") && ItemBool("Ultimate", targetObj.ChampionName) && SkillR.IsReady() && SkillR.InRange(targetObj.Position) && CanKill(targetObj, SkillR)) SkillR.CastOnUnit(targetObj, PacketCast());
            if (ItemBool("Combo", "Item") && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (ItemBool("Combo", "Ignite")) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, 450)).OrderBy(i => i.Health))
            {
                if (ItemBool("Clear", "Q") && SkillQ.IsReady())
                {
                    if (CanKill(Obj, SkillQ) && Player.Distance3D(Obj) <= Orbwalk.GetAutoAttackRange() + 50)
                    {
                        Orbwalk.SetAttack(false);
                        Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                        Orbwalk.SetAttack(true);
                        break;
                    }
                    else if (Player.Distance(Obj) > 500) SkillQ.Cast(PacketCast());
                }
                if (ItemBool("Clear", "E") && SkillE.IsReady() && !Player.HasBuff("GarenE") && !Player.HasBuff("GarenQBuff") && SkillE.InRange(Obj.Position)) SkillE.Cast(PacketCast());
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !SkillQ.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Orbwalk.GetAutoAttackRange() + 50) && CanKill(i, SkillQ)).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player)))
            {
                Orbwalk.SetAttack(false);
                Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                Orbwalk.SetAttack(true);
                break;
            }
        }
    }
}