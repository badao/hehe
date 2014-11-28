using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class Tryndamere : Program
    {
        public Tryndamere()
        {
            SkillQ = new Spell(SpellSlot.Q, 320);
            SkillW = new Spell(SpellSlot.W, 750);
            SkillE = new Spell(SpellSlot.E, 660);
            SkillR = new Spell(SpellSlot.R, 400);
            SkillE.SetSkillshot(SkillE.Instance.SData.SpellCastTime, SkillE.Instance.SData.LineWidth, SkillE.Instance.SData.MissileSpeed, false, SkillshotType.SkillshotLine);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("autoqusage", "Use Q If Hp Under").SetValue(new Slider(40, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E In Combo").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("autoeusage", "Use E If Hp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("killstealE", "Auto E To Kill Steal").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("surviveQ", "Try Use Q To Survive").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("surviveR", "Try Use R To Survive").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("CustomSkin", "Skin Changer").SetValue(new Slider(4, 0, 6))).ValueChanged += SkinChanger;

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
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalk.Mode.Combo:
                    NormalCombo();
                    break;
                case Orbwalk.Mode.Harass:
                    NormalCombo(true);
                    break;
                case Orbwalk.Mode.LaneClear:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.LaneFreeze:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.Flee:
                    if (SkillE.IsReady()) SkillE.Cast(Game.CursorPos, PacketCast());
                    break;
            }
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
            if (!(sender is Obj_SpellMissile)) return;
            var missle = (Obj_SpellMissile)sender;
            var caster = missle.SpellCaster;
            if (caster.IsEnemy)
            {
                if (Config.Item("surviveQ").GetValue<bool>() && SkillQ.IsReady())
                {
                    var HealthBuff = (Player.Mana == 100) ? new Int32[] { 80, 135, 190, 245, 300 }[SkillQ.Level - 1] + 1.5 * Player.FlatMagicDamageMod : (new Int32[] { 30, 40, 50, 60, 70 }[SkillQ.Level - 1] + 0.3 * Player.FlatMagicDamageMod + new double[] { 0.5, 0.95, 1.4, 1.85, 2.3 }[SkillQ.Level - 1] + 0.012 * Player.FlatMagicDamageMod) * Player.Mana;
                    if (missle.SData.Name.Contains("BasicAttack"))
                    {
                        if (missle.Target.IsMe && Player.Health <= caster.GetAutoAttackDamage(Player, true) && Player.Health + HealthBuff > caster.GetAutoAttackDamage(Player, true)) SkillQ.Cast();
                    }
                    else if (missle.Target.IsMe || missle.EndPosition.Distance(Player.Position) <= 130)
                    {
                        if (missle.SData.Name == "summonerdot")
                        {
                            if (Player.Health <= (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite) && Player.Health + HealthBuff > (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) SkillQ.Cast();
                        }
                        else if (Player.Health <= (caster as Obj_AI_Hero).GetSpellDamage(Player, (caster as Obj_AI_Hero).GetSpellSlot(missle.SData.Name, false), 1) && Player.Health + HealthBuff > (caster as Obj_AI_Hero).GetSpellDamage(Player, (caster as Obj_AI_Hero).GetSpellSlot(missle.SData.Name, false), 1)) SkillQ.Cast();
                    }
                }
                if (Config.Item("surviveR").GetValue<bool>() && SkillR.IsReady())
                {
                    if (missle.SData.Name.Contains("BasicAttack"))
                    {
                        if (missle.Target.IsMe && Player.Health <= caster.GetAutoAttackDamage(Player, true)) SkillR.Cast();
                    }
                    else if (missle.Target.IsMe || missle.EndPosition.Distance(Player.Position) <= 130)
                    {
                        if (missle.SData.Name == "summonerdot")
                        {
                            if (Player.Health <= (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) SkillR.Cast();
                        }
                        else if (Player.Health <= (caster as Obj_AI_Hero).GetSpellDamage(Player, (caster as Obj_AI_Hero).GetSpellSlot(missle.SData.Name, false), 1)) SkillR.Cast();
                    }
                }
            }
        }

        private void NormalCombo(bool IsHarass = false)
        {
            if (targetObj == null) return;
            if (Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady() && Player.Health * 100 / Player.MaxHealth <= Config.Item("autoqusage").GetValue<Slider>().Value && Player.CountEnemysInRange(800) >= 1) SkillQ.Cast(PacketCast());
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady() && SkillW.InRange(targetObj.Position))
            {
                if (Utility.IsBothFacing(Player, targetObj, 300))
                {
                    if (Player.GetAutoAttackDamage(targetObj) < targetObj.GetAutoAttackDamage(Player) || Player.Health < targetObj.Health) SkillW.Cast(PacketCast());
                }
                else if (Player.IsFacing(targetObj) && !targetObj.IsFacing(Player) && Player.Distance(targetObj) > 450) SkillW.Cast(PacketCast());
            }
            if (!IsHarass && Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && Player.Distance(targetObj) > 450)
            {
                if (Player.Health * 100 / Player.MaxHealth >= Config.Item("autoeusage").GetValue<Slider>().Value)
                {
                    SkillE.Cast(targetObj.Position + Vector3.Normalize(targetObj.Position - Player.Position) * 200, PacketCast());
                }
                else if (SkillR.IsReady() || (Player.Mana >= 70 && SkillQ.IsReady())) SkillE.Cast(targetObj.Position + Vector3.Normalize(targetObj.Position - Player.Position) * 200, PacketCast());
            }
            if (Config.Item("iusage").GetValue<bool>()) UseItem(targetObj);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillE.Range - 50, MinionTypes.All, MinionTeam.NotAlly);
            if (minionObj.Count == 0) return;
            var posEFarm = SkillE.GetLineFarmLocation(minionObj);
            if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady()) SkillE.Cast(posEFarm.MinionsHit >= 2 ? posEFarm.Position : minionObj.First().Position.To2D(), PacketCast());
        }

        private void KillSteal()
        {
            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillE.Range) && CanKill(i, SkillE) && i != targetObj);
            if (target != null && SkillE.IsReady()) SkillE.Cast(target.Position + Vector3.Normalize(target.Position - Player.Position) * 200, PacketCast());
        }

        private void UseItem(Obj_AI_Hero target)
        {
            if (Items.CanUseItem(Bilge) && Player.Distance(target) <= 450) Items.UseItem(Bilge, target);
            if (Items.CanUseItem(Blade) && Player.Distance(target) <= 450) Items.UseItem(Blade, target);
            if (Items.CanUseItem(Tiamat) && Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(target) < target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Items.CanUseItem(Youmuu) && Player.CountEnemysInRange(350) >= 1) Items.UseItem(Youmuu);
        }
    }
}