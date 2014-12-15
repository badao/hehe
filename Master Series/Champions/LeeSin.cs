using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class LeeSin : Program
    {
        private Obj_AI_Base allyObj = null;
        private bool WardCasted = false, JumpCasted = false, KickCasted = false, FlyCasted = false, FarmCasted = false, InsecJumpCasted = false, QCasted = false, WCasted = false, ECasted = false, RCasted = false;
        private enum HarassStage
        {
            Nothing,
            Doing,
            Finish
        }
        private HarassStage CurHarassStage = HarassStage.Nothing;
        private Vector3 HarassBackPos = default(Vector3), WardPlacePos = default(Vector3);
        private Spell Q2, E2;

        public LeeSin()
        {
            Q = new Spell(SpellSlot.Q, 1000);
            Q2 = new Spell(SpellSlot.Q, 1300);
            W = new Spell(SpellSlot.W, 700);
            E = new Spell(SpellSlot.E, 425);
            E2 = new Spell(SpellSlot.Q, 575);
            R = new Spell(SpellSlot.R, 375);
            Q.SetSkillshot(-0.5f, 60, 1800, true, SkillshotType.SkillshotLine);
            Q2.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);
            W.SetTargetted(0, 1500);
            E.SetSkillshot(-0.5f, 0, 0, false, SkillshotType.SkillshotCircle);
            E2.SetSkillshot(-0.5f, 0, 2000, false, SkillshotType.SkillshotCircle);
            R.SetTargetted(-0.5f, 1500);

            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWStarCombo", "Star Combo", true).SetValue(new KeyBind("X".ToCharArray()[0], KeyBindType.Press)));
            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWInsecCombo", "Insec", true).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWKSMob", "Kill Steal Mob", true).SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));
            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Passive", "Use Passive", false);
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "WSurvive", "-> To Survive", false);
                    ItemSlider(ComboMenu, "WUnder", "--> If Hp Under", 30);
                    ItemBool(ComboMenu, "WGap", "-> To Gap Closer");
                    ItemBool(ComboMenu, "WGapWard", "--> Ward Jump If No Ally Near", false);
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R If Killable");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemSlider(HarassMenu, "Q2Above", "-> Q2 If Hp Above", 20);
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemBool(HarassMenu, "WBackWard", "Ward Jump Back If No Ally Near", false);
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
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var InsecMenu = new Menu("Insec", "Insec");
                {
                    var InsecNearMenu = new Menu("Near Ally Config", "InsecNear");
                    {
                        ItemBool(InsecNearMenu, "ToChamp", "To Champion");
                        ItemSlider(InsecNearMenu, "ToChampHp", "-> If Hp Above", 20);
                        ItemSlider(InsecNearMenu, "ToChampR", "-> If In", 1100, 500, 1600);
                        ItemBool(InsecNearMenu, "DrawToChamp", "-> Draw Range", false);
                        ItemBool(InsecNearMenu, "ToTower", "To Tower");
                        ItemBool(InsecNearMenu, "ToMinion", "To Minion");
                        ItemSlider(InsecNearMenu, "ToMinionR", "-> If In", 1100, 500, 1600);
                        ItemBool(InsecNearMenu, "DrawToMinion", "-> Draw Range", false);
                        InsecMenu.AddSubMenu(InsecNearMenu);
                    }
                    ItemList(InsecMenu, "Mode", "Mode", new[] { "Near Ally", "Selected Ally", "Mouse Position" }, 2);
                    ItemBool(InsecMenu, "Flash", "Flash If Ward Jump Not Ready");
                    ItemBool(InsecMenu, "DrawLine", "Draw Insec Line");
                    ChampMenu.AddSubMenu(InsecMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    var KillableMenu = new Menu("Killable", "Killable");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy)) ItemBool(KillableMenu, Obj.ChampionName, "Use R On " + Obj.ChampionName);
                        UltiMenu.AddSubMenu(KillableMenu);
                    }
                    var InterruptMenu = new Menu("Interrupt", "Interrupt");
                    {
                        foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
                        {
                            foreach (var Spell in Interrupter.Spells.Where(i => i.ChampionName == Obj.ChampionName)) ItemBool(InterruptMenu, Obj.ChampionName + "_" + Spell.Slot.ToString(), "Spell " + Spell.Slot.ToString() + " Of " + Obj.ChampionName);
                        }
                        UltiMenu.AddSubMenu(InterruptMenu);
                    }
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "WJPink", "Ward Jump Use Pink Ward", false);
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit", false);
                    ItemBool(MiscMenu, "RInterrupt", "Use R To Interrupt");
                    ItemBool(MiscMenu, "InterruptGap", "-> Ward Jump If No Ally Near");
                    ItemBool(MiscMenu, "WSurvive", "Try Use W To Survive");
                    ItemBool(MiscMenu, "SmiteCol", "Auto Smite Collision");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 5, 0, 6).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Killable", "Killable Text", false);
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
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Game.OnWndProc += OnWndProc;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling()) return;
            if (ItemList("Insec", "Mode") == 1)
            {
                if (R.IsReady())
                {
                    allyObj = IsValid(allyObj, float.MaxValue, false) ? allyObj : null;
                }
                else if (allyObj != null) allyObj = null;
            }
            switch (Orbwalk.CurrentMode)
            {
                case Orbwalk.Mode.Combo:
                    NormalCombo();
                    break;
                case Orbwalk.Mode.Harass:
                    Harass();
                    break;
                case Orbwalk.Mode.LaneClear:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.LaneFreeze:
                    LaneJungClear();
                    break;
                case Orbwalk.Mode.LastHit:
                    LastHit();
                    break;
                case Orbwalk.Mode.Flee:
                    WardJump(Game.CursorPos);
                    break;
            }
            if (Orbwalk.CurrentMode != Orbwalk.Mode.Harass) CurHarassStage = HarassStage.Nothing;
            if (ItemActive("StarCombo")) StarCombo();
            if (ItemActive("InsecCombo"))
            {
                InsecCombo();
            }
            else InsecJumpCasted = false;
            if (ItemActive("KSMob")) KillStealMob();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Instance.Name == "BlindMonkQOne" ? Q.Range : Q2.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Instance.Name == "BlindMonkEOne" ? E.Range : E2.Range, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Insec", "DrawLine") && R.IsReady())
            {
                Byte validTargets = 0;
                if (targetObj != null)
                {
                    Utility.DrawCircle(targetObj.Position, 70, Color.FromArgb(0, 204, 0));
                    validTargets += 1;
                }
                if (GetInsecPos(true) != default(Vector3))
                {
                    Utility.DrawCircle(GetInsecPos(true), 70, Color.FromArgb(0, 204, 0));
                    validTargets += 1;
                }
                if (validTargets == 2) Drawing.DrawLine(Drawing.WorldToScreen(targetObj.Position), Drawing.WorldToScreen(targetObj.Position.To2D().Extend(GetInsecPos(true).To2D(), 600).To3D()), 1, Color.White);
            }
            if (ItemList("Insec", "Mode") == 0 && R.IsReady())
            {
                if (ItemBool("InsecNear", "ToChamp") && ItemBool("InsecNear", "DrawToChamp")) Utility.DrawCircle(Player.Position, ItemSlider("InsecNear", "ToChampR"), Color.White);
                if (ItemBool("InsecNear", "ToMinion") && ItemBool("InsecNear", "DrawToMinion")) Utility.DrawCircle(Player.Position, ItemSlider("InsecNear", "ToMinionR"), Color.White);
            }
            if (ItemBool("Draw", "Killable"))
            {
                foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i)))
                {
                    var dmgTotal = Player.GetAutoAttackDamage(Obj, true);
                    if (Q.IsReady())
                    {
                        if (Q.Instance.Name == "BlindMonkQOne")
                        {
                            dmgTotal += Q.GetDamage(Obj);
                        }
                        else if (Obj.HasBuff("BlindMonkSonicWave")) dmgTotal += Q.GetDamage(Obj, 1);
                    }
                    if (E.IsReady() && E.Instance.Name == "BlindMonkEOne") dmgTotal += E.GetDamage(Obj);
                    if (R.IsReady() && ItemBool("Killable", Obj.ChampionName)) dmgTotal += R.GetDamage(Obj);
                    if (Obj.Health + 5 <= dmgTotal)
                    {
                        var posText = Drawing.WorldToScreen(Obj.Position);
                        Drawing.DrawText(posText.X - 30, posText.Y - 5, Color.White, "Killable");
                    }
                }
            }
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "RInterrupt") || !ItemBool("Interrupt", (unit as Obj_AI_Hero).ChampionName + "_" + spell.Slot.ToString()) || Player.IsDead || !R.IsReady()) return;
            if (!R.InRange(unit.Position) && W.IsReady() && W.Instance.Name == "BlindMonkWOne")
            {
                var nearObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, W.Range + i.BoundingRadius, false) && !(i is Obj_AI_Turret) && i.Distance3D(unit) <= R.Range).OrderBy(i => i.Distance3D(unit));
                if (nearObj.Count() > 0 && !JumpCasted)
                {
                    foreach (var Obj in nearObj) W.CastOnUnit(Obj, PacketCast());
                }
                else if (ItemBool("Misc", "InterruptGap") && IsValid(unit, W.Range) && (GetWardSlot() != null || WardCasted)) WardJump(unit.Position);
            }
            if (IsValid(unit, R.Range)) R.CastOnUnit(unit, PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsMe)
            {
                if (args.SData.Name == "BlindMonkQOne")
                {
                    QCasted = true;
                    Utility.DelayAction.Add(Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze ? 2800 : 2200, () => QCasted = false);
                }
                if (args.SData.Name == "blindmonkqtwo")
                {
                    FlyCasted = true;
                    Utility.DelayAction.Add((int)(Player.Distance3D((Obj_AI_Base)args.Target) / Q.Speed * 1000 - 100) * 2, () => FlyCasted = false);
                }
                if (args.SData.Name == "BlindMonkWOne")
                {
                    WCasted = true;
                    Utility.DelayAction.Add(Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze ? 2800 : 1000, () => WCasted = false);
                    JumpCasted = true;
                    Utility.DelayAction.Add(1000, () => JumpCasted = false);
                }
                if (args.SData.Name == "BlindMonkEOne")
                {
                    ECasted = true;
                    Utility.DelayAction.Add(Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze ? 2800 : 2200, () => ECasted = false);
                }
                if (args.SData.Name == "BlindMonkRKick")
                {
                    RCasted = true;
                    Utility.DelayAction.Add(700, () => RCasted = false);
                    if (ItemActive("StarCombo") || ItemActive("InsecCombo"))
                    {
                        KickCasted = true;
                        Utility.DelayAction.Add(1000, () => KickCasted = false);
                    }
                }
            }
            else if (sender.IsEnemy && ItemBool("Misc", "WSurvive") && W.IsReady() && W.Instance.Name == "BlindMonkWOne")
            {
                if (args.Target.IsMe && ((Orbwalk.IsAutoAttack(args.SData.Name) && Player.Health <= sender.GetAutoAttackDamage(Player, true)) || (args.SData.Name == "summonerdot" && Player.Health <= (sender as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite))))
                {
                    W.Cast(PacketCast());
                    return;
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                        {
                            if (Player.Health <= (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i))
                            {
                                W.Cast(PacketCast());
                                return;
                            }
                        }
                    }
                }
            }
        }

        private void OnWndProc(WndEventArgs args)
        {
            if (args.WParam != 1 || MenuGUI.IsChatOpen || ItemList("Insec", "Mode") != 1 || !R.IsReady()) return;
            allyObj = null;
            if (Player.IsDead) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, 80, false, Game.CursorPos)).OrderBy(i => i.Position.Distance(Game.CursorPos))) allyObj = Obj;
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (ItemBool("Combo", "Passive") && Player.HasBuff("BlindMonkFlurry") && Orbwalk.InAutoAttackRange(targetObj) && Orbwalk.CanAttack()) return;
            if (ItemBool("Combo", "E") && E.IsReady())
            {
                if (E.Instance.Name == "BlindMonkEOne" && E.InRange(targetObj.Position))
                {
                    E.Cast(PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkTempest") && E2.InRange(targetObj.Position) && (Player.Distance3D(targetObj) > 450 || !ECasted)) E.Cast(PacketCast());
            }
            if (ItemBool("Combo", "Q") && Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(targetObj.Position))
                {
                    if (ItemBool("Misc", "SmiteCol"))
                    {
                        if (!SmiteCollision(targetObj, Q)) Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                    }
                    else Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj.Position))
                {
                    if (Player.Distance3D(targetObj) > 500 || CanKill(targetObj, Q2, 1) || (targetObj.HasBuff("BlindMonkTempest") && E.InRange(targetObj.Position) && !Orbwalk.InAutoAttackRange(targetObj)) || !QCasted) Q.Cast(PacketCast());
                }
            }
            if (ItemBool("Combo", "R") && ItemBool("Killable", targetObj.ChampionName) && R.IsReady() && R.InRange(targetObj.Position))
            {
                if (CanKill(targetObj, R) || (R.GetHealthPrediction(targetObj) - R.GetDamage(targetObj) + 5 <= GetQ2Dmg(targetObj, R.GetDamage(targetObj)) && ItemBool("Combo", "Q") && Q.IsReady() && targetObj.HasBuff("BlindMonkSonicWave"))) R.CastOnUnit(targetObj, PacketCast());
            }
            if (ItemBool("Combo", "W") && W.IsReady())
            {
                if (ItemBool("Combo", "WSurvive") || ItemBool("Combo", "WGap"))
                {
                    if (W.Instance.Name == "BlindMonkWOne")
                    {
                        if (ItemBool("Combo", "WSurvive") && Orbwalk.InAutoAttackRange(targetObj) && Player.HealthPercentage() <= ItemList("Combo", "WUnder"))
                        {
                            W.Cast(PacketCast());
                        }
                        else if (ItemBool("Combo", "WGap") && Player.Distance3D(targetObj) >= Orbwalk.GetAutoAttackRange() + 50 && !FlyCasted)
                        {
                            var jumpObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, W.Range + i.BoundingRadius, false) && !(i is Obj_AI_Turret) && i.Distance3D(targetObj) <= Orbwalk.GetAutoAttackRange() + 50).OrderBy(i => i.Distance3D(targetObj));
                            if (jumpObj.Count() > 0 && !JumpCasted)
                            {
                                foreach (var Obj in jumpObj) W.CastOnUnit(Obj, PacketCast());
                            }
                            else if (ItemBool("Combo", "WGapWard") && Player.Distance3D(targetObj) <= W.Range + Orbwalk.GetAutoAttackRange() + 50 && (GetWardSlot() != null || WardCasted)) WardJump(targetObj.Position);
                        }
                    }
                    else if (E.InRange(targetObj.Position) && !Player.HasBuff("BlindMonkSafeguard") && !WCasted) W.Cast(PacketCast());
                }
            }
            if (ItemBool("Combo", "Item")) UseItem(targetObj);
            if (ItemBool("Combo", "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (targetObj == null)
            {
                CurHarassStage = HarassStage.Nothing;
                return;
            }
            switch (CurHarassStage)
            {
                case HarassStage.Nothing:
                    CurHarassStage = HarassStage.Doing;
                    break;
                case HarassStage.Doing:
                    if (ItemBool("Harass", "Q") && Q.IsReady())
                    {
                        if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(targetObj.Position))
                        {
                            if (ItemBool("Misc", "SmiteCol"))
                            {
                                if (!SmiteCollision(targetObj, Q)) Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                            }
                            else Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                        }
                        else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj.Position) && (CanKill(targetObj, Q2, 1) || (W.IsReady() && W.Instance.Name == "BlindMonkWOne" && Player.Mana >= W.Instance.ManaCost + (ItemBool("Harass", "E") && E.IsReady() && E.Instance.Name == "BlindMonkEOne" ? Q.Instance.ManaCost + E.Instance.ManaCost : Q.Instance.ManaCost) && Player.HealthPercentage() >= ItemSlider("Harass", "Q2Above"))))
                        {
                            HarassBackPos = Player.ServerPosition;
                            Q.Cast(PacketCast());
                            Utility.DelayAction.Add((int)((Player.Distance3D(targetObj) + (ItemBool("Harass", "E") && E.IsReady() && E.Instance.Name == "BlindMonkEOne" ? E.Range : 0)) / Q.Speed * 1000 - 100) * 2, () => CurHarassStage = HarassStage.Finish);
                        }
                    }
                    if (ItemBool("Harass", "E") && E.IsReady())
                    {
                        if (E.Instance.Name == "BlindMonkEOne" && E.InRange(targetObj.Position))
                        {
                            E.Cast(PacketCast());
                        }
                        else if (targetObj.HasBuff("BlindMonkTempest") && E2.InRange(targetObj.Position)) CurHarassStage = HarassStage.Finish;
                    }
                    break;
                case HarassStage.Finish:
                    if (W.IsReady() && W.Instance.Name == "BlindMonkWOne")
                    {
                        var jumpObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, W.Range + i.BoundingRadius, false) && !(i is Obj_AI_Turret) && i.Distance3D(targetObj) >= 450).OrderByDescending(i => i.Distance3D(Player)).OrderBy(i => ObjectManager.Get<Obj_AI_Turret>().Where(a => IsValid(a, float.MaxValue, false)).OrderBy(a => a.Distance3D(Player)).FirstOrDefault().Distance3D(i));
                        if (jumpObj.Count() > 0 && !JumpCasted)
                        {
                            foreach (var Obj in jumpObj) W.CastOnUnit(Obj, PacketCast());
                        }
                        else if (ItemBool("Harass", "WBackWard") && (GetWardSlot() != null || WardCasted)) WardJump(HarassBackPos);
                    }
                    else
                    {
                        if (HarassBackPos != default(Vector3)) HarassBackPos = default(Vector3);
                        CurHarassStage = HarassStage.Nothing;
                    }
                    break;
            }
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q2.Range)).OrderBy(i => i.MaxHealth).Reverse();
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                var Passive = Player.HasBuff("BlindMonkFlurry");
                if (ItemBool("Clear", "Q") && Q.IsReady())
                {
                    if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(Obj.Position))
                    {
                        /*if (Obj.Team == GameObjectTeam.Neutral || (Obj.Team != GameObjectTeam.Neutral && ((Orbwalk.InAutoAttackRange(Obj) && Q.GetHealthPrediction(Obj) - Q.GetDamage(Obj) + 5 <= GetQ2Dmg(Obj, Q.GetDamage(Obj))) || (!Orbwalk.InAutoAttackRange(Obj) && (Player.Distance3D(Obj) > Orbwalk.GetAutoAttackRange() + 50 || CanKill(Obj, Q))))))*/
                        Q.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast());
                    }
                    else if (Obj.HasBuff("BlindMonkSonicWave") && (Q2.GetHealthPrediction(Obj) + 5 <= GetQ2Dmg(Obj) || Player.Distance3D(Obj) > 500 || !QCasted || !Passive)) Q.Cast(PacketCast());
                }
                if (ItemBool("Clear", "E") && E.IsReady())
                {
                    if (E.Instance.Name == "BlindMonkEOne" && !Passive && (minionObj.Count(i => E.InRange(i.Position)) >= 2 || (Obj.MaxHealth >= 1200 && E.InRange(Obj.Position))) && !FarmCasted)
                    {
                        E.Cast(PacketCast());
                        FarmCasted = true;
                        Utility.DelayAction.Add(300, () => FarmCasted = false);
                    }
                    else if (Obj.HasBuff("BlindMonkTempest") && E2.InRange(Obj.Position) && (!ECasted || !Passive)) E.Cast(PacketCast());
                }
                if (ItemBool("Clear", "W") && W.IsReady())
                {
                    if (W.Instance.Name == "BlindMonkWOne")
                    {
                        if (!Passive && Orbwalk.InAutoAttackRange(Obj) && !FarmCasted)
                        {
                            W.Cast(PacketCast());
                            FarmCasted = true;
                            Utility.DelayAction.Add(300, () => FarmCasted = false);
                        }
                    }
                    else if (E.InRange(Obj.Position) && (!WCasted || !Passive)) W.Cast(PacketCast());
                }
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady() || Q.Instance.Name != "BlindMonkQOne") return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q)).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player))) Q.CastIfHitchanceEquals(Obj, HitChance.VeryHigh, PacketCast());
        }

        private void WardJump(Vector3 Pos)
        {
            if (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || JumpCasted) return;
            bool Casted = false;
            var JumpPos = Pos;
            if (GetWardSlot() != null && !WardCasted && Player.Position.Distance(JumpPos) > GetWardRange()) JumpPos = Player.Position.To2D().Extend(JumpPos.To2D(), GetWardRange()).To3D();
            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, W.Range + i.BoundingRadius, false) && !(i is Obj_AI_Turret) && i.Position.Distance(WardCasted ? WardPlacePos : JumpPos) < 230 && (!ItemActive("InsecCombo") || (ItemActive("InsecCombo") && i.Name.EndsWith("Ward") && i is Obj_AI_Minion))).OrderBy(i => i.Position.Distance(WardCasted ? WardPlacePos : JumpPos)))
            {
                W.CastOnUnit(Obj, PacketCast());
                Casted = true;
                return;
            }
            if (!Casted && GetWardSlot() != null && !WardCasted)
            {
                GetWardSlot().UseItem(JumpPos);
                WardPlacePos = JumpPos;
                Utility.DelayAction.Add(800, () => WardPlacePos = default(Vector3));
                WardCasted = true;
                Utility.DelayAction.Add(800, () => WardCasted = false);
            }
        }

        private void StarCombo()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null) return;
            UseItem(targetObj);
            if (Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne" && Q.InRange(targetObj.Position))
                {
                    if (ItemBool("Misc", "SmiteCol"))
                    {
                        if (!SmiteCollision(targetObj, Q)) Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                    }
                    else Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj.Position) && (CanKill(targetObj, Q2, 1) || (!R.IsReady() && !RCasted && KickCasted) || (!R.IsReady() && !RCasted && !KickCasted && (Player.Distance3D(targetObj) > 600 || !QCasted)))) Q.Cast(PacketCast());
            }
            if (W.IsReady())
            {
                if (W.Instance.Name == "BlindMonkWOne")
                {
                    if (R.IsReady())
                    {
                        if (Q.IsReady() && targetObj.HasBuff("BlindMonkSonicWave") && !R.InRange(targetObj.Position) && Player.Distance3D(targetObj) < W.Range + R.Range - 200) WardJump(targetObj.Position);
                    }
                    else if (Orbwalk.InAutoAttackRange(targetObj)) W.Cast(PacketCast());
                }
                else if (E.InRange(targetObj.Position) && !Player.HasBuff("BlindMonkSafeguard") && !WCasted) W.Cast(PacketCast());
            }
            if (R.IsReady() && Q.IsReady() && targetObj.HasBuff("BlindMonkSonicWave") && R.InRange(targetObj.Position)) R.CastOnUnit(targetObj, PacketCast());
            if (E.IsReady() && !R.IsReady())
            {
                if (E.Instance.Name == "BlindMonkEOne" && E.InRange(targetObj.Position))
                {
                    E.Cast(PacketCast());
                }
                else if (targetObj.HasBuff("BlindMonkTempest") && E2.InRange(targetObj.Position) && (Player.Distance3D(targetObj) > 450 || !ECasted)) E.Cast(PacketCast());
            }
        }

        private void InsecCombo()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null) return;
            if (GetInsecPos() != default(Vector3))
            {
                if (R.InRange(targetObj.Position) && (GetInsecPos(true).Distance(targetObj.Position) - GetInsecPos(true).Distance(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 500).To3D())) / 500 > 0.7)
                {
                    R.CastOnUnit(targetObj, PacketCast());
                    return;
                }
                if (W.IsReady() && W.Instance.Name == "BlindMonkWOne" && (GetWardSlot() != null || WardCasted) && Player.Position.Distance(GetInsecPos()) < GetWardRange())
                {
                    WardJump(GetInsecPos());
                    if (ItemBool("Insec", "Flash")) InsecJumpCasted = true;
                    return;
                }
                if (ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && !WardCasted)
                {
                    var jumpObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, W.Range + i.BoundingRadius, false) && !(i is Obj_AI_Turret) && i.Position.Distance(GetInsecPos()) < 400).OrderBy(i => i.Position.Distance(GetInsecPos()));
                    if (jumpObj.Count() > 0 && !FlyCasted && !Q.IsReady())
                    {
                        foreach (var Obj in jumpObj)
                        {
                            if (Player.Position.Distance(GetInsecPos()) < Player.Distance3D(Obj) + Obj.Position.Distance(GetInsecPos()) && W.IsReady() && W.Instance.Name == "BlindMonkWOne")
                            {
                                W.CastOnUnit(Obj, PacketCast());
                                Utility.DelayAction.Add((int)((Player.Position.Distance(GetInsecPos()) - Obj.Position.Distance(GetInsecPos())) / W.Speed * 1000 + 300), () => CastFlash(GetInsecPos()));
                                return;
                            }
                        }
                    }
                    else if (Player.Position.Distance(GetInsecPos()) < 400)
                    {
                        CastFlash(GetInsecPos());
                        return;
                    }
                }
            }
            if (Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne")
                {
                    if (Q.InRange(targetObj.Position) && Q.GetPrediction(targetObj).Hitchance >= HitChance.VeryHigh)
                    {
                        if (ItemBool("Misc", "SmiteCol"))
                        {
                            if (!SmiteCollision(targetObj, Q)) Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                        }
                        else Q.CastIfHitchanceEquals(targetObj, HitChance.VeryHigh, PacketCast());
                    }
                    else if (GetInsecPos() != default(Vector3) && Q.GetPrediction(targetObj).Hitchance <= HitChance.OutOfRange)
                    {
                        foreach (var Obj in Q.GetPrediction(targetObj).CollisionObjects.Where(i => i.Position.Distance(GetInsecPos()) < ((ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || GetWardSlot() == null || !WardCasted)) ? 400 : GetWardRange()) && !CanKill(i, Q)).OrderBy(i => i.Position.Distance(GetInsecPos()))) Q.CastIfHitchanceEquals(Obj, HitChance.VeryHigh, PacketCast());
                    }
                }
                else if (targetObj.HasBuff("BlindMonkSonicWave") && Q2.InRange(targetObj.Position) && (CanKill(targetObj, Q2, 1) || (!R.IsReady() && !RCasted && KickCasted) || (!R.IsReady() && !RCasted && !KickCasted && (Player.Distance3D(targetObj) > 600 || !QCasted)) || (GetInsecPos() != default(Vector3) && Player.Position.Distance(GetInsecPos()) > ((ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || GetWardSlot() == null || !WardCasted)) ? 400 : GetWardRange()))))
                {
                    Q.Cast(PacketCast());
                }
                else if (GetInsecPos() != default(Vector3) && ObjectManager.Get<Obj_AI_Base>().Any(i => i.HasBuff("BlindMonkSonicWave") && IsValid(i, Q2.Range) && i.Position.Distance(GetInsecPos()) < ((ItemBool("Insec", "Flash") && FlashReady() && !InsecJumpCasted && (!W.IsReady() || W.Instance.Name != "BlindMonkWOne" || GetWardSlot() == null || !WardCasted)) ? 400 : GetWardRange()))) Q.Cast(PacketCast());
            }
        }

        private void KillStealMob()
        {
            var Mob = ObjectManager.Get<Obj_AI_Minion>().FirstOrDefault(i => IsValid(i, Q2.Range) && i.Team == GameObjectTeam.Neutral && new string[] { "SRU_Baron", "SRU_Dragon", "SRU_Blue", "SRU_Red" }.Any(a => i.Name.StartsWith(a) && !i.Name.StartsWith(a + "Mini")));
            CustomOrbwalk(Mob);
            if (Mob == null) return;
            if (SmiteReady()) CastSmite(Mob);
            if (Q.IsReady())
            {
                if (Q.Instance.Name == "BlindMonkQOne")
                {
                    if (Q.InRange(Mob.Position) && Q.GetHealthPrediction(Mob) - Q.GetDamage(Mob) - (SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0) + 5 <= GetQ2Dmg(Mob, Q.GetDamage(Mob) + (SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0)) && Q.GetPrediction(Mob).Hitchance >= HitChance.VeryHigh)
                    {
                        Q.CastIfHitchanceEquals(Mob, HitChance.VeryHigh, PacketCast());
                    }
                    else if (SmiteReady() && Q2.GetHealthPrediction(Mob) + 5 <= Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) && Q.GetPrediction(Mob).Hitchance <= HitChance.OutOfRange)
                    {
                        foreach (var Obj in Q.GetPrediction(Mob).CollisionObjects.Where(i => i.Distance3D(Mob) <= 760 && !CanKill(i, Q)).OrderBy(i => i.Distance3D(Mob))) Q.CastIfHitchanceEquals(Obj, HitChance.VeryHigh, PacketCast());
                    }
                }
                else if (Mob.HasBuff("BlindMonkSonicWave") && Q2.GetHealthPrediction(Mob) - (SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0) + 5 <= GetQ2Dmg(Mob, SmiteReady() ? Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite) : 0))
                {
                    Q.Cast(PacketCast());
                    if (SmiteReady()) Utility.DelayAction.Add((int)((Player.Distance3D(Mob) - 760) / Q.Speed * 1000 + 300), () => CastSmite(Mob, false));
                }
                else if (ObjectManager.Get<Obj_AI_Base>().Any(i => i.HasBuff("BlindMonkSonicWave") && IsValid(i, Q2.Range) && i.Distance3D(Mob) <= 760) && SmiteReady() && Q2.GetHealthPrediction(Mob) + 5 <= Player.GetSummonerSpellDamage(Mob, Damage.SummonerSpell.Smite))
                {
                    Q.Cast(PacketCast());
                    Utility.DelayAction.Add((int)((Player.Distance3D(Mob) - 760) / Q.Speed * 1000 + 300), () => CastSmite(Mob));
                }
            }
        }

        private Vector3 GetInsecPos(bool IsDraw = false)
        {
            if (!R.IsReady()) return default(Vector3);
            switch (ItemList("Insec", "Mode"))
            {
                case 0:
                    var ChampList = ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, ItemSlider("InsecNear", "ToChampR"), false) && i.HealthPercentage() >= ItemSlider("InsecNear", "ToChampHp")).ToList();
                    var TowerObj = ObjectManager.Get<Obj_AI_Turret>().Where(i => IsValid(i, float.MaxValue, false)).OrderBy(i => i.Distance3D(Player)).FirstOrDefault();
                    var MinionObj = targetObj != null ? ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, ItemSlider("InsecNear", "ToMinionR"), false) && Player.Distance3D(TowerObj) > 1500 && i.Distance3D(targetObj) > 600 && !i.Name.EndsWith("Ward")).OrderByDescending(i => i.Distance3D(targetObj)).OrderBy(i => i.Distance3D(TowerObj)).FirstOrDefault() : null;
                    if (ChampList.Count > 0 && ItemBool("InsecNear", "ToChamp"))
                    {
                        var Pos = default(Vector2);
                        foreach (var Obj in ChampList) Pos += Obj.Position.To2D();
                        Pos = new Vector2(Pos.X / ChampList.Count, Pos.Y / ChampList.Count);
                        return IsDraw ? Pos.To3D() : Pos.Extend(targetObj.Position.To2D(), targetObj.Position.To2D().Distance(Pos) + 220).To3D();
                    }
                    if (MinionObj != null && ItemBool("InsecNear", "ToMinion")) return IsDraw ? MinionObj.Position : MinionObj.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(MinionObj) + 220).To3D();
                    if (TowerObj != null && ItemBool("InsecNear", "ToTower")) return IsDraw ? TowerObj.Position : TowerObj.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(TowerObj) + 220).To3D();
                    break;
                case 1:
                    if (allyObj != null) return IsDraw ? allyObj.Position : allyObj.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(allyObj) + 220).To3D();
                    break;
                case 2:
                    return IsDraw ? Game.CursorPos : Game.CursorPos.To2D().Extend(targetObj.Position.To2D(), targetObj.Position.Distance(Game.CursorPos) + 220).To3D();
            }
            return default(Vector3);
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
        }

        private double GetQ2Dmg(Obj_AI_Base Target, double Plus = 0)
        {
            var Dmg = Player.CalcDamage(Target, Damage.DamageType.Physical, new double[] { 50, 80, 110, 140, 170 }[Q.Level - 1] + 0.9 * Player.FlatPhysicalDamageMod + 0.08 * (Target.MaxHealth - Target.Health + Plus));
            return Target is Obj_AI_Minion && Dmg > 400 ? Player.CalcDamage(Target, Damage.DamageType.Physical, 400) : Dmg;
        }
    }
}