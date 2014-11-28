using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class DrMundo : Program
    {
        public DrMundo()
        {
            SkillQ = new Spell(SpellSlot.Q, 1100);
            SkillW = new Spell(SpellSlot.W, 325);
            SkillE = new Spell(SpellSlot.E, 300);
            SkillR = new Spell(SpellSlot.R, 20);
            SkillQ.SetSkillshot(SkillQ.Instance.SData.SpellCastTime, SkillQ.Instance.SData.LineWidth - 20, SkillQ.Instance.SData.MissileSpeed, true, SkillshotType.SkillshotLine);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("autowusage", "Use W If Hp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearW", "Use W").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearAutoW", "Use W If Hp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Ultimate", "useUlt"));
            Config.SubMenu("useUlt").AddItem(new MenuItem("surviveR", "Try Use R To Survive").SetValue(true));
            Config.SubMenu("useUlt").AddItem(new MenuItem("autouseR", "Use R If Hp Under").SetValue(new Slider(35, 1)));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("lasthitQ", "Use Q To Last Hit").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("killstealQ", "Auto Q To Kill Steal").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("SmiteCol", "Auto Smite Collision Minion").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("CustomSkin", "Skin Changer").SetValue(new Slider(7, 0, 7))).ValueChanged += SkinChanger;

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawQ", "Q Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawW", "W Range").SetValue(true));

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
            if (Config.Item("killstealQ").GetValue<bool>()) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item("DrawQ").GetValue<bool>() && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, SkillQ.Range, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawW").GetValue<bool>() && SkillW.Level > 0) Utility.DrawCircle(Player.Position, SkillW.Range, SkillW.IsReady() ? Color.Green : Color.Red);
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
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady() && Player.HasBuff("BurningAgony") && Player.CountEnemysInRange(500) == 0) SkillW.Cast();
            if (targetObj == null) return;
            if (Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady())
            {
                if (Config.Item("SmiteCol").GetValue<bool>() && SkillQ.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                {
                    if (!SmiteCollision(targetObj, SkillQ)) SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                }
                else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
            }
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady())
            {
                if (Player.Health * 100 / Player.MaxHealth >= Config.Item("autowusage").GetValue<Slider>().Value)
                {
                    if (SkillW.InRange(targetObj.Position))
                    {
                        if (!Player.HasBuff("BurningAgony")) SkillW.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("BurningAgony")) SkillW.Cast(PacketCast());
                }
                else if (Player.HasBuff("BurningAgony")) SkillW.Cast(PacketCast());
            }
            if (Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && Orbwalk.InAutoAttackRange(targetObj)) SkillE.Cast(PacketCast());
            if (Config.Item("iusage").GetValue<bool>() && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null)
            {
                if (Config.Item("useClearW").GetValue<bool>() && SkillW.IsReady() && Player.HasBuff("BurningAgony")) SkillW.Cast(PacketCast());
                return;
            }
            if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady() && Orbwalk.InAutoAttackRange(minionObj)) SkillE.Cast(PacketCast());
            if (Config.Item("useClearW").GetValue<bool>() && SkillW.IsReady())
            {
                if (Player.Health * 100 / Player.MaxHealth >= Config.Item("useClearAutoW").GetValue<Slider>().Value)
                {
                    if (MinionManager.GetMinions(Player.Position, SkillW.Range + 100, MinionTypes.All, MinionTeam.NotAlly).Count >= 2)
                    {
                        if (!Player.HasBuff("BurningAgony")) SkillW.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("BurningAgony")) SkillW.Cast(PacketCast());
                }
                else if (Player.HasBuff("BurningAgony")) SkillW.Cast(PacketCast());
            }
            if (Config.Item("useClearQ").GetValue<bool>() && SkillQ.IsReady() && CanKill(minionObj, SkillQ)) SkillQ.CastIfHitchanceEquals(minionObj, HitChance.VeryHigh, PacketCast());
        }

        private void LastHit()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly).Where(i => CanKill(i, SkillQ)).OrderByDescending(i => i.Distance(Player)).FirstOrDefault();
            if (minionObj != null && SkillQ.IsReady()) SkillQ.CastIfHitchanceEquals(minionObj, HitChance.VeryHigh, PacketCast());
        }

        private void KillSteal()
        {
            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillQ.Range) && CanKill(i, SkillQ) && i != targetObj);
            if (target != null && SkillQ.IsReady())
            {
                if (Config.Item("SmiteCol").GetValue<bool>() && SkillQ.GetPrediction(target).Hitchance == HitChance.Collision)
                {
                    if (!SmiteCollision(target, SkillQ)) SkillQ.CastIfHitchanceEquals(target, HitChance.VeryHigh, PacketCast());
                }
                else SkillQ.CastIfHitchanceEquals(target, HitChance.VeryHigh, PacketCast());
            }
        }
    }
}