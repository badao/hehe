using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using LX_Orbwalker;

namespace Master
{
    class LeeSin : Program
    {
        private const String Version = "1.1.5";
        private Obj_AI_Base allyObj = null;
        private bool WardCasted = false, InsecCasted = false, QCasted = false, WCasted = false, ECasted = false, RCasted = false;

        public LeeSin()
        {
            SkillQ = new Spell(SpellSlot.Q, 1100);//1300
            SkillW = new Spell(SpellSlot.W, 700);
            SkillE = new Spell(SpellSlot.E, 425);//575
            SkillR = new Spell(SpellSlot.R, 375);
            SkillQ.SetSkillshot(SkillQ.Instance.SData.SpellCastTime, SkillQ.Instance.SData.LineWidth - 20, SkillQ.Instance.SData.MissileSpeed, true, SkillshotType.SkillshotLine);

            Config.SubMenu("Orbwalker").SubMenu("lxOrbwalker_Modes").AddItem(new MenuItem(Name + "starActive", "Star Combo").SetValue(new KeyBind("X".ToCharArray()[0], KeyBindType.Press)));
            Config.SubMenu("Orbwalker").SubMenu("lxOrbwalker_Modes").AddItem(new MenuItem(Name + "insecMake", "Insec").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
            Config.SubMenu("Orbwalker").SubMenu("lxOrbwalker_Modes").AddItem(new MenuItem(Name + "ksbrdr", "Kill Steal Baron/Dragon").SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));

            Config.AddSubMenu(new Menu("Combo", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "pusage", "Use Passive").SetValue(false));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "autowusage", "Use W If Hp Under").SetValue(new Slider(40, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "rusage", "Use R To Finish").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Harass", "hsettings"));
            Config.SubMenu("hsettings").AddItem(new MenuItem(Name + "harMode", "Use Harass If Hp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("hsettings").AddItem(new MenuItem(Name + "useHarE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearW", "Use W").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearE", "Use E").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearI", "Use Tiamat/Hydra Item").SetValue(true));

            Config.AddSubMenu(new Menu("Insec", "insettings"));
            Config.SubMenu("insettings").AddItem(new MenuItem(Name + "insecMode", "Mode").SetValue(new StringList(new[] { "Near Object", "Selected Object", "Mouse Position" })));
            Config.SubMenu("insettings").AddItem(new MenuItem(Name + "insectower", "To Tower If No Champion In").SetValue(new Slider(1100, 500, 1500)));
            Config.SubMenu("insettings").AddItem(new MenuItem(Name + "insecFlash", "Flash If Ward Jump Not Ready").SetValue(true));

            Config.AddSubMenu(new Menu("Ultimate", "useUlt"));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
            {
                Config.SubMenu("useUlt").AddItem(new MenuItem(Name + "ult" + enemy.ChampionName, "Use Ultimate On " + enemy.ChampionName).SetValue(true));
            }

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "lasthitQ", "Use Q To Last Hit").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "surviveW", "Try Use W To Survive").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "smite", "Auto Smite Collision Minion").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "SkinID", "Skin Changer").SetValue(new Slider(4, 0, 6))).ValueChanged += SkinChanger;
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "packetCast", "Use Packet To Cast").SetValue(true));

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "drawInsec", "Insec Line").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "drawInsecTower", "Insec To Tower Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "drawKillable", "Killable Text").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawQ", "Q Range").SetValue(false));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawW", "W Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawE", "E Range").SetValue(false));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawR", "R Range").SetValue(false));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Game.OnWndProc += OnWndProc;
            Obj_AI_Base.OnCreate += OnCreate;
            Game.PrintChat("<font color = \"#33CCCC\">Master of {0}</font> <font color = \"#fff8e7\">Brian v{1}</font>", Name, Version);
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead) return;
            PacketCast = Config.Item(Name + "packetCast").GetValue<bool>();
            Ward = GetWardSlot();
            if (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex == 0) allyObj = GetInsecAlly();
            switch (LXOrbwalker.CurrentMode)
            {
                case LXOrbwalker.Mode.Combo:
                    NormalCombo();
                    break;
                case LXOrbwalker.Mode.Harass:
                    Harass();
                    break;
                case LXOrbwalker.Mode.LaneClear:
                    LaneJungClear();
                    break;
                case LXOrbwalker.Mode.LaneFreeze:
                    LaneJungClear();
                    break;
                case LXOrbwalker.Mode.Lasthit:
                    if (Config.Item(Name + "lasthitQ").GetValue<bool>()) LastHit();
                    break;
                case LXOrbwalker.Mode.Flee:
                    WardJump(Game.CursorPos);
                    break;
            }
            LXOrbwalker.CustomOrbwalkMode = false;
            if (Config.Item(Name + "insecMake").GetValue<KeyBind>().Active)
            {
                LXOrbwalker.CustomOrbwalkMode = true;
                InsecCombo();
            }
            if (Config.Item(Name + "starActive").GetValue<KeyBind>().Active)
            {
                LXOrbwalker.CustomOrbwalkMode = true;
                StarCombo();
            }
            if (Config.Item(Name + "ksbrdr").GetValue<KeyBind>().Active)
            {
                LXOrbwalker.CustomOrbwalkMode = true;
                KillStealBrDr();
            }
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item(Name + "DrawQ").GetValue<bool>() && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, (SkillQ.Instance.Name == "BlindMonkQOne") ? SkillQ.Range : 1300, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawW").GetValue<bool>() && SkillW.Level > 0) Utility.DrawCircle(Player.Position, SkillW.Range, SkillW.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, (SkillE.Instance.Name == "BlindMonkEOne") ? SkillE.Range : 575, SkillE.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawR").GetValue<bool>() && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "drawInsec").GetValue<bool>() && SkillR.IsReady())
            {
                Byte validTargets = 0;
                if (targetObj != null)
                {
                    Utility.DrawCircle(targetObj.Position, 70, Color.FromArgb(0, 204, 0));
                    validTargets += 1;
                }
                if (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex == 2 || (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex != 2 && allyObj != null))
                {
                    Utility.DrawCircle((Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex != 2) ? allyObj.Position : Game.CursorPos, 70, Color.FromArgb(0, 204, 0));
                    if (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex != 2) validTargets += 1;
                }
                if ((Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex == 2 && validTargets == 1) || (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex != 2 && validTargets == 2))
                {
                    var posDraw = targetObj.Position + Vector3.Normalize(((Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex != 2) ? allyObj.Position : Game.CursorPos) - targetObj.Position) * 600;
                    Drawing.DrawLine(Drawing.WorldToScreen(targetObj.Position), Drawing.WorldToScreen(posDraw), 2, Color.White);
                }
            }
            if (Config.Item(Name + "drawInsecTower").GetValue<bool>() && Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex == 0 && SkillR.IsReady()) Utility.DrawCircle(Player.Position, Config.Item(Name + "insectower").GetValue<Slider>().Value, Color.Purple);
            if (Config.Item(Name + "drawKillable").GetValue<bool>())
            {
                foreach (var killableObj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget()))
                {
                    var dmgTotal = Player.GetAutoAttackDamage(killableObj);
                    if (SkillQ.IsReady() && SkillQ.Instance.Name == "BlindMonkQOne") dmgTotal += SkillQ.GetDamage(killableObj);
                    if (SkillR.IsReady() && Config.Item(Name + "ult" + killableObj.ChampionName).GetValue<bool>()) dmgTotal += SkillR.GetDamage(killableObj);
                    if (SkillE.IsReady() && SkillQ.Instance.Name == "BlindMonkEOne") dmgTotal += SkillE.GetDamage(killableObj);
                    if (SkillQ.IsReady() && (killableObj.HasBuff("BlindMonkQOne", true) || killableObj.HasBuff("blindmonkqonechaos", true))) dmgTotal += GetQ2Dmg(killableObj, dmgTotal);
                    if (killableObj.Health <= dmgTotal)
                    {
                        var posText = Drawing.WorldToScreen(killableObj.Position);
                        Drawing.DrawText(posText.X - 30, posText.Y - 5, Color.White, "Killable");
                    }
                }
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "BlindMonkQOne")
            {
                QCasted = true;
                Utility.DelayAction.Add(2000, () => QCasted = false);
            }
            if (args.SData.Name == "BlindMonkWOne")
            {
                WCasted = true;
                Utility.DelayAction.Add((LXOrbwalker.CurrentMode == LXOrbwalker.Mode.LaneClear || LXOrbwalker.CurrentMode == LXOrbwalker.Mode.LaneFreeze) ? 2000 : 1000, () => WCasted = false);
            }
            if (args.SData.Name == "BlindMonkEOne")
            {
                ECasted = true;
                Utility.DelayAction.Add(2000, () => ECasted = false);
            }
            if (args.SData.Name == "BlindMonkRKick")
            {
                RCasted = true;
                Utility.DelayAction.Add(700, () => RCasted = false);
                if (Config.Item(Name + "insecMake").GetValue<KeyBind>().Active)
                {
                    InsecCasted = true;
                    Utility.DelayAction.Add(2000, () => InsecCasted = false);
                }
                else InsecCasted = false;
            }
        }

        private void OnWndProc(WndEventArgs args)
        {
            if (MenuGUI.IsChatOpen || Player.IsDead) return;
            if (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex == 1 && args.Msg == (uint)WindowsMessages.WM_LBUTTONDOWN)
            {
                allyObj = null;
                foreach (var obj in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsAlly && !i.IsMe && !i.IsDead && i.Distance(Game.CursorPos) <= 130)) allyObj = obj;
            }
        }

        private void OnCreate(GameObject sender, EventArgs args)
        {
            if (sender is Obj_SpellMissile && sender.IsValid && Config.Item(Name + "surviveW").GetValue<bool>() && SkillW.IsReady() && SkillW.Instance.Name == "BlindMonkWOne")
            {
                var missle = (Obj_SpellMissile)sender;
                var caster = missle.SpellCaster;
                if (caster.IsEnemy)
                {
                    var ShieldBuff = new Int32[] { 40, 80, 120, 160, 200 }[SkillW.Level - 1] + 0.8 * Player.FlatMagicDamageMod;
                    if (LXOrbwalker.IsAutoAttack(missle.SData.Name) && !LXOrbwalker.IsAutoAttackReset(missle.SData.Name))
                    {
                        if (missle.Target.IsMe && Player.Health <= caster.GetAutoAttackDamage(Player, true) && Player.Health + ShieldBuff > caster.GetAutoAttackDamage(Player, true)) SkillW.CastOnUnit(Player, PacketCast);
                    }
                    else if (missle.Target.IsMe || missle.EndPosition.Distance(Player.Position) <= 130)
                    {
                        if (missle.SData.Name == "summonerdot")
                        {
                            if (Player.Health <= (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite) && Player.Health + ShieldBuff > (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) SkillW.CastOnUnit(Player, PacketCast);
                        }
                        else if (Player.Health <= caster.GetDamageSpell(Player, missle.SData.Name).CalculatedDamage && Player.Health + ShieldBuff > caster.GetDamageSpell(Player, missle.SData.Name).CalculatedDamage) SkillW.CastOnUnit(Player, PacketCast);
                    }
                }
            }
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item(Name + "pusage").GetValue<bool>() && Player.HasBuff("blindmonkpassive_cosmetic", true) && LXOrbwalker.InAutoAttackRange(targetObj) && LXOrbwalker.CanAttack()) return;
            if (Config.Item(Name + "eusage").GetValue<bool>() && SkillE.IsReady() && SkillE.Instance.Name == "BlindMonkEOne" && SkillE.InRange(targetObj.Position)) SkillE.Cast();
            if (Config.Item(Name + "qusage").GetValue<bool>() && SkillQ.IsReady() && SkillQ.Instance.Name == "BlindMonkQOne")
            {
                if (Config.Item(Name + "smite").GetValue<bool>() && SkillQ.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                {
                    if (CheckingCollision(targetObj, SkillQ))
                    {
                        SkillQ.Cast(SkillQ.GetPrediction(targetObj).CastPosition, PacketCast);
                    }
                    else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
                }
                else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
            }
            if (Config.Item(Name + "qusage").GetValue<bool>() && SkillQ.IsReady() && targetObj.IsValidTarget(1300) && (targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)))
            {
                if (Player.Distance(targetObj) > 500 || SkillQ.IsKillable(targetObj, 1) || (targetObj.HasBuff("BlindMonkEOne", true) && SkillE.InRange(targetObj.Position)) || !QCasted) SkillQ.Cast();
            }
            if (Config.Item(Name + "eusage").GetValue<bool>() && SkillE.IsReady() && targetObj.IsValidTarget(575) && targetObj.HasBuff("BlindMonkEOne", true))
            {
                if (Player.Distance(targetObj) > 450 || !ECasted) SkillE.Cast();
            }
            if (Config.Item(Name + "rusage").GetValue<bool>() && Config.Item(Name + "ult" + targetObj.ChampionName).GetValue<bool>() && SkillR.IsReady() && SkillR.InRange(targetObj.Position))
            {
                if ((SkillR.GetHealthPrediction(targetObj) > 1 && SkillR.GetHealthPrediction(targetObj) < 2.5) || (targetObj.Health - SkillR.GetDamage(targetObj) <= GetQ2Dmg(targetObj, SkillR.GetDamage(targetObj)) && SkillQ.IsReady() && (targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)) && Player.Mana >= 50)) SkillR.CastOnUnit(targetObj, PacketCast);
            }
            if (Config.Item(Name + "wusage").GetValue<bool>() && SkillW.IsReady() && SkillE.InRange(targetObj.Position) && Player.Health * 100 / Player.MaxHealth <= Config.Item(Name + "autowusage").GetValue<Slider>().Value)
            {
                if (SkillW.Instance.Name == "BlindMonkWOne")
                {
                    SkillW.CastOnUnit(Player, PacketCast);
                }
                else if (!Player.HasBuff("blindmonkwoneshield", true) && !WCasted) SkillW.Cast();
            }
            if (Config.Item(Name + "iusage").GetValue<bool>()) UseItem(targetObj);
            if (Config.Item(Name + "ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (targetObj == null) return;
            var jumpObj = ObjectManager.Get<Obj_AI_Base>().Where(i => !i.IsMe && i.IsAlly && !(i is Obj_AI_Turret) && i.ServerPosition.Distance(Player.ServerPosition) <= SkillW.Range + i.BoundingRadius).OrderByDescending(i => i.ServerPosition.Distance(Player.ServerPosition)).OrderBy(i => i.Distance(ObjectManager.Get<Obj_AI_Turret>().Where(a => a.IsAlly && !a.IsDead).OrderBy(a => a.Distance(Player)).First()));
            if (SkillQ.IsReady())
            {
                if (SkillQ.Instance.Name == "BlindMonkQOne")
                {
                    if (Config.Item(Name + "smite").GetValue<bool>() && SkillQ.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                    {
                        if (CheckingCollision(targetObj, SkillQ))
                        {
                            SkillQ.Cast(SkillQ.GetPrediction(targetObj).CastPosition, PacketCast);
                        }
                        else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
                    }
                    else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
                }
                else if ((targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)) && targetObj.IsValidTarget(1300) && (SkillQ.IsKillable(targetObj, 1) || (SkillW.IsReady() && SkillW.Instance.Name == "BlindMonkWOne" && Player.Mana >= (Config.Item(Name + "useHarE").GetValue<bool>() ? 130 : 80) && Player.Health * 100 / Player.MaxHealth >= Config.Item(Name + "harMode").GetValue<Slider>().Value && jumpObj.Count() >= 1))) SkillQ.Cast();
            }
            if (!SkillQ.IsReady() && Config.Item(Name + "useHarE").GetValue<bool>() && SkillE.IsReady() && SkillE.Instance.Name == "BlindMonkEOne" && SkillE.InRange(targetObj.Position)) SkillE.Cast();
            if (!SkillQ.IsReady() && SkillW.IsReady() && SkillW.Instance.Name == "BlindMonkWOne" && ((SkillE.Level == 0 && Utility.CountEnemysInRange(200) >= 1) || (Config.Item(Name + "useHarE").GetValue<bool>() && targetObj.HasBuff("BlindMonkEOne", true)) || (!Config.Item(Name + "useHarE").GetValue<bool>() && Utility.CountEnemysInRange(200) >= 1)) && !WCasted) SkillW.CastOnUnit(jumpObj.First(), PacketCast);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null) return;
            if (Player.HasBuff("blindmonkpassive_cosmetic", true) && LXOrbwalker.InAutoAttackRange(minionObj) && LXOrbwalker.CanAttack()) return;
            if (Config.Item(Name + "useClearE").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(minionObj.Position))
            {
                if (SkillE.Instance.Name == "BlindMonkEOne")
                {
                    SkillE.Cast();
                }
                else if (minionObj.HasBuff("BlindMonkEOne", true) && minionObj.IsValidTarget(575) && !ECasted) SkillE.Cast();
            }
            if (Config.Item(Name + "useClearQ").GetValue<bool>() && SkillQ.IsReady())
            {
                if (SkillQ.Instance.Name == "BlindMonkQOne")
                {
                    SkillQ.CastIfHitchanceEquals(minionObj, HitChance.VeryHigh, PacketCast);
                }
                else if ((minionObj.HasBuff("BlindMonkQOne", true) || minionObj.HasBuff("blindmonkqonechaos", true)) && (SkillQ.IsKillable(minionObj, 1) || !QCasted || Player.Distance(minionObj) > 600)) SkillQ.Cast();
            }
            if (Config.Item(Name + "useClearW").GetValue<bool>() && SkillW.IsReady() && LXOrbwalker.InAutoAttackRange(minionObj))
            {
                if (SkillW.Instance.Name == "BlindMonkWOne")
                {
                    SkillW.CastOnUnit(Player, PacketCast);
                }
                else if (!WCasted) SkillW.Cast();
            }
            if (Config.Item(Name + "useClearI").GetValue<bool>() && Player.Distance(minionObj) <= 350)
            {
                if (Items.CanUseItem(Tiamat)) Items.UseItem(Tiamat);
                if (Items.CanUseItem(Hydra)) Items.UseItem(Hydra);
            }
        }

        private void LastHit()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault(i => SkillQ.IsKillable(i) && Player.Distance(i) > LXOrbwalker.GetAutoAttackRange(Player, i) + 150);
            if (minionObj != null && SkillQ.IsReady() && SkillQ.Instance.Name == "BlindMonkQOne") SkillQ.CastIfHitchanceEquals(minionObj, HitChance.VeryHigh, PacketCast);
        }

        private void WardJump(Vector3 Pos)
        {
            if ((SkillW.IsReady() && SkillW.Instance.Name != "BlindMonkWOne") || !SkillW.IsReady()) return;
            bool Jumped = false;
            if (Player.Distance(Pos) > SkillW.Range) Pos = Player.Position + Vector3.Normalize(Pos - Player.Position) * 600;
            foreach (var jumpObj in ObjectManager.Get<Obj_AI_Base>().Where(i => !i.IsMe && i.IsAlly && !(i is Obj_AI_Turret) && i.ServerPosition.Distance(Pos) <= (Config.Item(Name + "insecMake").GetValue<KeyBind>().Active ? 130 : 230)))
            {
                Jumped = true;
                if (jumpObj.ServerPosition.Distance(Player.ServerPosition) <= SkillW.Range + jumpObj.BoundingRadius && !WCasted) SkillW.CastOnUnit(jumpObj, PacketCast);
                return;
            }
            if (!Jumped && Ward != null && !WardCasted)
            {
                Ward.UseItem(Pos);
                WardCasted = true;
                Utility.DelayAction.Add(1000, () => WardCasted = false);
            }
        }

        private void StarCombo()
        {
            LXOrbwalker.Orbwalk(Game.CursorPos, targetObj);
            if (targetObj == null) return;
            if (SkillE.IsReady() && SkillE.Instance.Name == "BlindMonkEOne" && SkillE.InRange(targetObj.Position)) SkillE.Cast();
            if (SkillQ.IsReady())
            {
                if (SkillQ.Instance.Name == "BlindMonkQOne")
                {
                    if (Config.Item(Name + "smite").GetValue<bool>() && SkillQ.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                    {
                        if (CheckingCollision(targetObj, SkillQ))
                        {
                            SkillQ.Cast(SkillQ.GetPrediction(targetObj).CastPosition, PacketCast);
                        }
                        else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
                    }
                    else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
                }
                else if (targetObj.IsValidTarget(1300) && (targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)) && SkillQ.IsKillable(targetObj, 1)) SkillQ.Cast();
            }
            if (!SkillR.InRange(targetObj.Position) && SkillR.IsReady() && SkillQ.IsReady() && (targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)) && Player.Distance(targetObj) <= SkillW.Range + SkillR.Range - 150 && SkillW.IsReady() && SkillW.Instance.Name == "BlindMonkWOne" && !WCasted) WardJump(targetObj.Position);
            UseItem(targetObj);
            if (SkillR.IsReady() && SkillQ.IsReady() && (targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)) && SkillR.InRange(targetObj.Position) && Player.Mana >= 50) SkillR.CastOnUnit(targetObj, PacketCast);
            if (!SkillR.IsReady() && SkillQ.IsReady() && targetObj.IsValidTarget(1300) && (targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)) && (SkillQ.IsKillable(targetObj, 1) || !RCasted)) SkillQ.Cast();
            if (!SkillR.IsReady() && SkillE.IsReady() && targetObj.IsValidTarget(575) && targetObj.HasBuff("BlindMonkEOne", true) && (Player.Distance(targetObj) > 450 || !ECasted)) SkillE.Cast();
            CastIgnite(targetObj);
        }

        private void InsecCombo()
        {
            LXOrbwalker.Orbwalk(Game.CursorPos, targetObj);
            if (targetObj == null || (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex != 2 && allyObj == null)) return;
            if (SkillR.IsReady())
            {
                Vector3 posKickTo = (Config.Item(Name + "insecMode").GetValue<StringList>().SelectedIndex != 2) ? allyObj.Position : Game.CursorPos;
                Vector3 posJumpTo = posKickTo + Vector3.Normalize(targetObj.Position - posKickTo) * (targetObj.Distance(posKickTo) + 250);
                if (SkillR.InRange(targetObj.Position) && Player.Distance(posJumpTo) <= 100)
                {
                    SkillR.CastOnUnit(targetObj, PacketCast);
                    return;
                }
                if (Config.Item(Name + "insecFlash").GetValue<bool>())
                {
                    if (SkillW.IsReady() && SkillW.Instance.Name == "BlindMonkWOne" && !WCasted && Player.Distance(posJumpTo) < 600)
                    {
                        WardJump(posJumpTo);
                        return;
                    }
                    if (FlashReady())
                    {
                        var Obj = ObjectManager.Get<Obj_AI_Base>().Where(i => !i.IsMe && i.IsAlly && !(i is Obj_AI_Turret) && i.ServerPosition.Distance(Player.ServerPosition) <= SkillW.Range + i.BoundingRadius && i.Distance(posJumpTo) < 600).OrderBy(i => i.Distance(posJumpTo)).FirstOrDefault();
                        if (Obj != null && Player.Distance(posJumpTo) < 1300)
                        {
                            if (SkillW.IsReady() && SkillW.Instance.Name == "BlindMonkWOne")
                            {
                                if (!WCasted) SkillW.CastOnUnit(Obj, PacketCast);
                                if (WCasted) Utility.DelayAction.Add(1000, () => CastFlash(posJumpTo));
                                return;
                            }
                        }
                        else if (!WCasted && Player.Distance(posJumpTo) >= 300 && Player.Distance(posJumpTo) < 600)
                        {
                            CastFlash(posJumpTo);
                            return;
                        }
                    }
                }
                else if (SkillW.IsReady() && SkillW.Instance.Name == "BlindMonkWOne" && !WCasted && Player.Distance(posJumpTo) < 600)
                {
                    WardJump(posJumpTo);
                    return;
                }
            }
            if (SkillQ.IsReady())
            {
                if (SkillQ.Instance.Name == "BlindMonkQOne")
                {
                    if (Config.Item(Name + "smite").GetValue<bool>() && SkillQ.GetPrediction(targetObj).Hitchance == HitChance.Collision)
                    {
                        if (CheckingCollision(targetObj, SkillQ))
                        {
                            SkillQ.Cast(SkillQ.GetPrediction(targetObj).CastPosition, PacketCast);
                        }
                        else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
                    }
                    else SkillQ.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast);
                }
                else
                {
                    if ((targetObj.HasBuff("BlindMonkQOne", true) || targetObj.HasBuff("blindmonkqonechaos", true)) && targetObj.IsValidTarget(1300))
                    {
                        if (Player.Distance(targetObj) > 600 || SkillQ.IsKillable(targetObj, 1) || (!SkillR.IsReady() && !RCasted && InsecCasted) || !QCasted) SkillQ.Cast();
                    }
                    else
                    {
                        var enemyObj = ObjectManager.Get<Obj_AI_Base>().FirstOrDefault(i => i.IsValidTarget(1300) && i.Distance(targetObj) < 600 && (i.HasBuff("BlindMonkQOne", true) || i.HasBuff("blindmonkqonechaos", true)));
                        if (enemyObj != null && (Player.Distance(enemyObj) > 600 || !QCasted)) SkillQ.Cast();
                    }
                }
            }
        }

        private void KillStealBrDr()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, 1500, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault(i => i.Name == "Worm12.1.1" || i.Name == "Dragon6.1.1");
            LXOrbwalker.Orbwalk(Game.CursorPos, minionObj);
            if (minionObj == null) return;
            if (SkillQ.IsReady() && !SmiteReady() && minionObj.Health - SkillQ.GetDamage(minionObj) <= GetQ2Dmg(minionObj, SkillQ.GetDamage(minionObj)))
            {
                if (SkillQ.Instance.Name == "BlindMonkQOne")
                {
                    SkillQ.CastIfHitchanceEquals(minionObj, HitChance.VeryHigh, PacketCast);
                }
                else if ((minionObj.HasBuff("BlindMonkQOne", true) || minionObj.HasBuff("blindmonkqonechaos", true)) && minionObj.IsValidTarget(1300)) SkillQ.Cast();
            }
            if (SkillQ.IsReady() && SmiteReady() && minionObj.Health - (SkillQ.GetDamage(minionObj) + Player.GetSummonerSpellDamage(minionObj, Damage.SummonerSpell.Smite)) <= GetQ2Dmg(minionObj, SkillQ.GetDamage(minionObj) + Player.GetSummonerSpellDamage(minionObj, Damage.SummonerSpell.Smite)))
            {
                if (SkillQ.Instance.Name == "BlindMonkQOne")
                {
                    SkillQ.CastIfHitchanceEquals(minionObj, HitChance.VeryHigh, PacketCast);
                }
                else if ((minionObj.HasBuff("BlindMonkQOne", true) || minionObj.HasBuff("blindmonkqonechaos", true)) && minionObj.IsValidTarget(1300))
                {
                    SkillQ.Cast();
                    CastSmite(minionObj);
                }
            }
            CastSmite(minionObj);
        }

        private Obj_AI_Base GetInsecAlly()
        {
            Obj_AI_Base nearObj = null;
            if (ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsAlly && !i.IsDead && !i.IsMe && i.Distance(Player) < Config.Item(Name + "insectower").GetValue<Slider>().Value) != null)
            {
                nearObj = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsAlly && !i.IsDead && !i.IsMe).OrderBy(i => i.Distance(Player)).First();
            }
            else
            {
                nearObj = ObjectManager.Get<Obj_AI_Turret>().Where(i => i.IsAlly && !i.IsDead).OrderBy(i => i.Distance(Player)).First();
                var nearMinion = (targetObj != null) ? ObjectManager.Get<Obj_AI_Minion>().Where(i => i.IsAlly && !i.IsDead && i.Distance(Player) < 1600).OrderByDescending(i => i.Distance(targetObj)).FirstOrDefault() : null;
                if (Player.Distance(nearObj) > 1500 && nearMinion != null) nearObj = nearMinion;
            }
            return nearObj;
        }

        private void UseItem(Obj_AI_Hero target)
        {
            if (Items.CanUseItem(Bilge) && Player.Distance(target) <= 450) Items.UseItem(Bilge, target);
            if (Items.CanUseItem(Blade) && Player.Distance(target) <= 450) Items.UseItem(Blade, target);
            if (Items.CanUseItem(Tiamat) && Utility.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && (Utility.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(target) < target.Health && Utility.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Rand) && Utility.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
        }

        private double GetQ2Dmg(Obj_AI_Base target, double dmgPlus)
        {
            var Dmg = Player.CalcDamage(target, Damage.DamageType.Physical, new Int32[] { 50, 80, 110, 140, 170 }[SkillQ.Level - 1] + 0.9 * Player.FlatPhysicalDamageMod + 0.08 * (target.MaxHealth - (target.Health - dmgPlus)));
            return (target is Obj_AI_Minion && Dmg > 400) ? Player.CalcDamage(target, Damage.DamageType.Physical, 400) : Dmg;
        }
    }
}