using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class Nasus : Program
    {
        private Int32 Sheen = 3057, Iceborn = 3025;

        public Nasus()
        {
            SkillQ = new Spell(SpellSlot.Q, 300);
            SkillW = new Spell(SpellSlot.W, 600);
            SkillE = new Spell(SpellSlot.E, 650);
            SkillR = new Spell(SpellSlot.R, 20);
            SkillW.SetTargetted(SkillW.Instance.SData.SpellCastTime, SkillW.Instance.SData.MissileSpeed);
            SkillE.SetSkillshot(SkillE.Instance.SData.SpellCastTime, SkillE.Instance.SData.LineWidth, SkillE.Instance.SData.MissileSpeed, false, SkillshotType.SkillshotCircle);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Ultimate", "useUlt"));
            Config.SubMenu("useUlt").AddItem(new MenuItem("surviveR", "Try Use R To Survive").SetValue(true));
            Config.SubMenu("useUlt").AddItem(new MenuItem("autouseR", "Use R If Hp Under").SetValue(new Slider(30, 1)));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("lasthitQ", "Use Q To Last Hit").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("killstealE", "Auto E To Kill Steal").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("CustomSkin", "Skin Changer").SetValue(new Slider(5, 0, 5))).ValueChanged += SkinChanger;

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawW", "W Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawE", "E Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            GameObject.OnCreate += OnCreate;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit && Config.Item("lasthitQ").GetValue<bool>()) LastHit();
            if (Config.Item("killstealE").GetValue<bool>()) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item("DrawW").GetValue<bool>() && SkillW.Level > 0) Utility.DrawCircle(Player.Position, SkillW.Range, SkillW.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
        }

        private void OnCreate(GameObject sender, EventArgs args)
        {
            if (sender is Obj_SpellMissile && sender.IsValid && Config.Item("surviveR").GetValue<bool>() && SkillR.IsReady())
            {
                var missle = (Obj_SpellMissile)sender;
                var caster = missle.SpellCaster;
                if (caster.IsEnemy)
                {
                    if (missle.SData.Name.Contains("BasicAttack"))
                    {
                        if (missle.Target.IsMe && (Player.Health - caster.GetAutoAttackDamage(Player, true)) * 100 / Player.MaxHealth <= Config.Item("autouseR").GetValue<Slider>().Value) SkillR.Cast();
                    }
                    else if (missle.Target.IsMe || missle.EndPosition.Distance(Player.Position) <= 130)
                    {
                        if (missle.SData.Name == "summonerdot")
                        {
                            if ((Player.Health - (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) * 100 / Player.MaxHealth <= Config.Item("autouseR").GetValue<Slider>().Value) SkillR.Cast();
                        }
                        else if ((Player.Health - (caster as Obj_AI_Hero).GetSpellDamage(Player, (caster as Obj_AI_Hero).GetSpellSlot(missle.SData.Name, false), 1)) * 100 / Player.MaxHealth <= Config.Item("autouseR").GetValue<Slider>().Value) SkillR.Cast();
                    }
                }
            }
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady() && SkillW.InRange(targetObj.Position)) SkillW.CastOnUnit(targetObj, PacketCast());
            if (Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(targetObj.Position)) SkillE.Cast(targetObj.Position + Vector3.Normalize(targetObj.Position - Player.Position) * 100, PacketCast());
            if (Config.Item("qusage").GetValue<bool>() && Orbwalk.InAutoAttackRange(targetObj))
            {
                var DmgAA = Player.GetAutoAttackDamage(targetObj) * Math.Floor(SkillQ.Instance.Cooldown / (1 / (Player.PercentMultiplicativeAttackSpeedMod * 0.638)));
                if ((targetObj.Health <= GetBonusDmg(targetObj) || targetObj.Health > DmgAA + GetBonusDmg(targetObj)) && (SkillQ.IsReady() || Player.HasBuff("NasusQ", true)))
                {
                    Orbwalk.SetAttack(false);
                    if (!Player.HasBuff("NasusQ", true)) SkillQ.Cast(PacketCast());
                    if (Player.HasBuff("NasusQ", true)) Player.IssueOrder(GameObjectOrder.AttackUnit, targetObj);
                    Orbwalk.SetAttack(true);
                }
            }
            if (Config.Item("iusage").GetValue<bool>() && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var towerObj = (Obj_AI_Base)ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(i => i.IsValidTarget() && Orbwalk.InAutoAttackRange(i) && i.Health <= GetBonusDmg(i));
            var minionObj = MinionManager.GetMinions(Player.Position, SkillE.Range - 50, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            var Obj = (towerObj != null) ? towerObj : minionObj.FirstOrDefault(i => i.IsValidTarget() && Orbwalk.InAutoAttackRange(i));
            if (Config.Item("useClearQ").GetValue<bool>() && Obj != null)
            {
                var DmgAA = Player.GetAutoAttackDamage(Obj) * Math.Floor(SkillQ.Instance.Cooldown / (1 / (Player.PercentMultiplicativeAttackSpeedMod * 0.638)));
                if ((Obj.Health <= GetBonusDmg(Obj) || Obj.Health > DmgAA + GetBonusDmg(Obj)) && (SkillQ.IsReady() || Player.HasBuff("NasusQ", true)))
                {
                    Orbwalk.SetAttack(false);
                    if (!Player.HasBuff("NasusQ", true)) SkillQ.Cast(PacketCast());
                    if (Player.HasBuff("NasusQ", true)) Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                    Orbwalk.SetAttack(true);
                }
            }
            if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady() && minionObj.Count > 0)
            {
                var posEFarm = SkillE.GetCircularFarmLocation(minionObj);
                SkillE.Cast(posEFarm.MinionsHit >= 2 ? posEFarm.Position : minionObj.First().Position.To2D(), PacketCast());
            }
        }

        private void LastHit()
        {
            if (SkillQ.IsReady() || Player.HasBuff("NasusQ", true))
            {
                Orbwalk.SetAttack(false);
                var minionObj = (Obj_AI_Base)ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(i => i.IsValidTarget() && Orbwalk.InAutoAttackRange(i) && i.Health <= GetBonusDmg(i));
                if (minionObj == null) minionObj = MinionManager.GetMinions(Player.Position, 500, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth).FirstOrDefault(i => Orbwalk.InAutoAttackRange(i) && i.Health <= GetBonusDmg(i));
                if (minionObj != null)
                {
                    if (!Player.HasBuff("NasusQ", true)) SkillQ.Cast(PacketCast());
                    if (Player.HasBuff("NasusQ", true)) Player.IssueOrder(GameObjectOrder.AttackUnit, minionObj);
                }
                Orbwalk.SetAttack(true);
            }
        }

        private void KillSteal()
        {
            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillE.Range) && CanKill(i, SkillE) && i != targetObj);
            if (target != null && SkillE.IsReady()) SkillE.Cast(target.Position, PacketCast());
        }

        private double GetBonusDmg(Obj_AI_Base target)
        {
            double DmgItem = 0;
            if (Items.HasItem(Sheen) && (Items.CanUseItem(Sheen) || Player.HasBuff("sheen", true)) && Player.BaseAttackDamage > DmgItem) DmgItem = Player.BaseAttackDamage;
            if (Items.HasItem(Iceborn) && (Items.CanUseItem(Iceborn) || Player.HasBuff("itemfrozenfist", true)) && Player.BaseAttackDamage * 1.25 > DmgItem) DmgItem = Player.BaseAttackDamage * 1.25;
            return (SkillQ.IsReady() || Player.HasBuff("NasusQ", true)) ? SkillQ.GetDamage(target) : 0 + Player.GetAutoAttackDamage(target) + ((DmgItem > 0) ? Player.CalcDamage(target, Damage.DamageType.Physical, DmgItem) : 0);
        }
    }
}