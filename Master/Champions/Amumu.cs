using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class Amumu : Program
    {
        public Amumu()
        {
            SkillQ = new Spell(SpellSlot.Q, 1100);
            SkillW = new Spell(SpellSlot.W, 300);
            SkillE = new Spell(SpellSlot.E, 350);
            SkillR = new Spell(SpellSlot.R, 550);
            SkillQ.SetSkillshot(SkillQ.Instance.SData.SpellCastTime, SkillQ.Instance.SData.LineWidth - 20, SkillQ.Instance.SData.MissileSpeed, true, SkillshotType.SkillshotLine);
            SkillE.SetSkillshot(SkillE.Instance.SData.SpellCastTime, SkillE.Instance.SData.LineWidth, SkillE.Instance.SData.MissileSpeed, false, SkillshotType.SkillshotCircle);
            SkillR.SetSkillshot(SkillR.Instance.SData.SpellCastTime, SkillR.Instance.SData.LineWidth, SkillR.Instance.SData.MissileSpeed, false, SkillshotType.SkillshotCircle);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("autowusage", "Use W If Mp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("rusage", "Use R").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("ruseMode", "R Mode").SetValue(new StringList(new[] { "Finish", "# Enemy" })));
            Config.SubMenu("csettings").AddItem(new MenuItem("rmulti", "Use R If Enemy Above").SetValue(new Slider(2, 1, 4)));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearW", "Use W").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearAutoW", "Use W If Mp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("useAntiQ", "Use Q To Anti Gap Closer").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("SmiteCol", "Auto Smite Collision Minion").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("CustomSkin", "Skin Changer").SetValue(new Slider(6, 0, 7))).ValueChanged += SkinChanger;

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawQ", "Q Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawW", "W Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawE", "E Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawR", "R Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) LaneJungClear();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item("DrawQ").GetValue<bool>() && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, SkillQ.Range, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawW").GetValue<bool>() && SkillW.Level > 0) Utility.DrawCircle(Player.Position, SkillW.Range, SkillW.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawR").GetValue<bool>() && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!Config.Item("useAntiQ").GetValue<bool>()) return;
            if (gapcloser.Sender.IsValidTarget(SkillQ.Range) && SkillQ.IsReady() && Player.Distance(gapcloser.Sender) < 400) SkillQ.Cast(gapcloser.Sender, PacketCast());
        }

        private void NormalCombo()
        {
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady() && Player.HasBuff("AuraofDespair") && Player.CountEnemysInRange(500) == 0) SkillW.Cast(PacketCast());
            if (targetObj == null) return;
            if (Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady() && (CanKill(targetObj, SkillQ) || !Orbwalk.InAutoAttackRange(targetObj)))
            {
                if (Config.Item("SmiteCol").GetValue<bool>() && SkillQ.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                {
                    if (!SmiteCollision(targetObj, SkillQ)) SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                }
                else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
            }
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady())
            {
                if (Player.Mana * 100 / Player.MaxMana >= Config.Item("autowusage").GetValue<Slider>().Value)
                {
                    if (SkillW.InRange(targetObj.Position))
                    {
                        if (!Player.HasBuff("AuraofDespair")) SkillW.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("AuraofDespair")) SkillW.Cast(PacketCast());
                }
                else if (Player.HasBuff("AuraofDespair")) SkillW.Cast(PacketCast());
            }
            if (Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast());
            if (Config.Item("rusage").GetValue<bool>() && SkillR.IsReady())
            {
                switch (Config.Item("ruseMode").GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        if (SkillR.InRange(targetObj.Position) && CanKill(targetObj, SkillR)) SkillR.Cast(PacketCast());
                        break;
                    case 1:
                        if (SkillQ.IsReady())
                        {
                            var UltiObj = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillQ.Range) && i.CountEnemysInRange((int)SkillR.Range) >= Config.Item("rmulti").GetValue<Slider>().Value);
                            if (UltiObj != null)
                            {
                                if (Config.Item("SmiteCol").GetValue<bool>() && SkillQ.GetPrediction(UltiObj).Hitchance == HitChance.Collision)
                                {
                                    if (!SmiteCollision(UltiObj, SkillQ)) SkillQ.CastIfHitchanceEquals(UltiObj, HitChance.VeryHigh, PacketCast());
                                }
                                else SkillQ.CastIfHitchanceEquals(UltiObj, HitChance.VeryHigh, PacketCast());
                            }
                        }
                        else if (Player.CountEnemysInRange((int)SkillR.Range) >= Config.Item("rmulti").GetValue<Slider>().Value) SkillR.Cast(PacketCast());
                        break;
                }
            }
            if (Config.Item("iusage").GetValue<bool>() && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null)
            {
                if (Config.Item("useClearW").GetValue<bool>() && SkillW.IsReady() && Player.HasBuff("AuraofDespair")) SkillW.Cast(PacketCast());
                return;
            }
            if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(minionObj.Position)) SkillE.Cast(PacketCast());
            if (Config.Item("useClearW").GetValue<bool>() && SkillW.IsReady())
            {
                if (Player.Mana * 100 / Player.MaxMana >= Config.Item("useClearAutoW").GetValue<Slider>().Value)
                {
                    if (MinionManager.GetMinions(Player.Position, SkillW.Range + 100, MinionTypes.All, MinionTeam.NotAlly).Count >= 2)
                    {
                        if (!Player.HasBuff("AuraofDespair")) SkillW.Cast(PacketCast());
                    }
                    else if (Player.HasBuff("AuraofDespair")) SkillW.Cast(PacketCast());
                }
                else if (Player.HasBuff("AuraofDespair")) SkillW.Cast(PacketCast());
            }
            if (Config.Item("useClearQ").GetValue<bool>() && SkillQ.IsReady() && (Player.Distance(minionObj) > 450 || CanKill(minionObj, SkillQ))) SkillQ.CastIfHitchanceEquals(minionObj, HitChance.VeryHigh, PacketCast());
        }
    }
}