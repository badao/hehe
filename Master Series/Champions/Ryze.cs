using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = Master.Common.M_Orbwalker;

namespace Master.Champions
{
    class Ryze : Program
    {
        public Ryze()
        {
            SkillQ = new Spell(SpellSlot.Q, 625);
            SkillW = new Spell(SpellSlot.W, 600);
            SkillE = new Spell(SpellSlot.E, 600);
            SkillR = new Spell(SpellSlot.R, 200);
            SkillQ.SetTargetted(0, 1400);
            SkillW.SetTargetted(0, 500);
            SkillE.SetTargetted(0, 1000);

            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem(Name + "_OW_Chase", "Chase").SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));
            var ChampMenu = new Menu("Plugin", Name + "_Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemSlider(ComboMenu, "QDelay", "-> Stop All If Will Ready In ? ms", 500, 300, 700);
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ItemBool(ClearMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ItemBool(MiscMenu, "Exploit", "-> Tear Exploit");
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemBool(MiscMenu, "WAntiGap", "Use W To Anti Gap Closer");
                    ItemBool(MiscMenu, "WInterrupt", "Use W To Interrupt");
                    ItemBool(MiscMenu, "SeraphSurvive", "Try Use Seraph's Embrace To Survive");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 7, 0, 8).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "W", "W Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.BeforeAttack += BeforeAttack;
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit)
            {
                LastHit();
            }
            else if (ItemActive("Chase")) NormalCombo("Chase");
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, SkillQ.Range, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && SkillW.Level > 0) Utility.DrawCircle(Player.Position, SkillW.Range, SkillW.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "WAntiGap") || Player.IsDead || !SkillW.IsReady()) return;
            if (IsValid(gapcloser.Sender, SkillW.Range - 200)) SkillW.CastOnUnit(gapcloser.Sender, PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "WInterrupt") || Player.IsDead || !SkillW.IsReady()) return;
            if (IsValid(unit, SkillW.Range)) SkillW.CastOnUnit(unit, PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsEnemy && ItemBool("Misc", "SeraphSurvive") && Items.CanUseItem(3040))
            {
                if (args.Target.IsMe && ((Orbwalk.IsAutoAttack(args.SData.Name) && Player.Health <= sender.GetAutoAttackDamage(Player, true)) || (args.SData.Name == "summonerdot" && Player.Health <= (sender as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite))))
                {
                    Items.UseItem(3040);
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                        {
                            if (Player.Health <= (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i)) Items.UseItem(3040);
                        }
                    }
                }
            }
        }

        private void BeforeAttack(Orbwalk.BeforeAttackEventArgs Args)
        {
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                if ((ItemBool(Orbwalk.CurrentMode.ToString(), "Q") && SkillQ.IsReady() && SkillQ.InRange(Args.Target.Position)) || (ItemBool(Orbwalk.CurrentMode.ToString(), "W") && SkillW.IsReady() && SkillW.InRange(Args.Target.Position)) || (ItemBool(Orbwalk.CurrentMode.ToString(), "E") && SkillE.IsReady() && SkillE.InRange(Args.Target.Position))) Args.Process = false;
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                if ((ItemBool("Clear", "Q") && SkillQ.IsReady() && SkillQ.InRange(Args.Target.Position)) || (ItemBool("Clear", "W") && SkillW.IsReady() && SkillW.InRange(Args.Target.Position)) || (ItemBool("Clear", "E") && SkillE.IsReady() && SkillE.InRange(Args.Target.Position))) Args.Process = false;
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit && ItemBool("Misc", "QLastHit") && SkillQ.IsReady() && SkillQ.InRange(Args.Target.Position)) Args.Process = false;
        }

        private void NormalCombo(string Mode)
        {
            if (Mode == "Chase") CustomOrbwalk(targetObj);
            if (targetObj == null) return;
            if ((Mode == "Chase" || (Mode != "Chase" && ItemBool(Mode, "Q"))) && SkillQ.IsReady() && SkillQ.InRange(targetObj.Position) && CanKill(targetObj, SkillQ)) SkillQ.CastOnUnit(targetObj, PacketCast());
            if ((Mode == "Chase" || (Mode != "Chase" && ItemBool(Mode, "E"))) && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && CanKill(targetObj, SkillE)) SkillE.CastOnUnit(targetObj, PacketCast());
            if ((Mode == "Chase" || (Mode != "Chase" && ItemBool(Mode, "W"))) && SkillW.IsReady() && SkillW.InRange(targetObj.Position) && (CanKill(targetObj, SkillW) || (Player.Distance3D(targetObj) > SkillW.Range - 20 && !targetObj.IsFacing(Player)))) SkillW.CastOnUnit(targetObj, PacketCast());
            switch (Mode)
            {
                case "Harass":
                    if (ItemBool(Mode, "Q") && SkillQ.IsReady() && SkillQ.InRange(targetObj.Position)) SkillQ.CastOnUnit(targetObj, PacketCast());
                    if (ItemBool(Mode, "W") && SkillW.IsReady() && SkillW.InRange(targetObj.Position)) SkillW.CastOnUnit(targetObj, PacketCast());
                    if (ItemBool(Mode, "E") && SkillE.IsReady() && SkillE.InRange(targetObj.Position)) SkillE.CastOnUnit(targetObj, PacketCast());
                    break;
                case "Combo":
                    if (ItemBool(Mode, "Ignite")) CastIgnite(targetObj);
                    if (ItemBool(Mode, "Q") && SkillQ.IsReady() && SkillQ.InRange(targetObj.Position)) SkillQ.CastOnUnit(targetObj, PacketCast());
                    if (!ItemBool(Mode, "Q") || (ItemBool(Mode, "Q") && !SkillQ.IsReady()))
                    {
                        if (ItemBool(Mode, "Q") && SkillQ.IsReady(ItemSlider(Mode, "QDelay")) && Math.Abs(Player.PercentCooldownMod) >= 0.2) return;
                        if (ItemBool(Mode, "R") && SkillR.IsReady() && (Math.Abs(Player.PercentCooldownMod) < 0.2 || (Math.Abs(Player.PercentCooldownMod) >= 0.2 && Player.LastCastedSpellName() == "Overload")) && (Player.HealthPercentage() <= 40 || Player.CountEnemysInRange((int)SkillQ.Range + 200) == 1 || Player.CountEnemysInRange((int)SkillQ.Range + 300) >= 2)) SkillR.Cast(PacketCast());
                        if ((!ItemBool(Mode, "R") || (ItemBool(Mode, "R") && !SkillR.IsReady())) && ItemBool(Mode, "W") && SkillW.IsReady() && SkillW.InRange(targetObj.Position) && (Math.Abs(Player.PercentCooldownMod) < 0.2 || (Math.Abs(Player.PercentCooldownMod) >= 0.2 && (Player.LastCastedSpellName() == "Overload" || (ItemBool(Mode, "R") && !SkillR.IsReady() && Player.LastCastedSpellName() == "DesperatePower" && Player.HasBuff("DesperatePower")))))) SkillW.CastOnUnit(targetObj, PacketCast());
                        if ((!ItemBool(Mode, "R") || (ItemBool(Mode, "R") && !SkillR.IsReady())) && (!ItemBool(Mode, "W") || (ItemBool(Mode, "W") && !SkillW.IsReady())) && ItemBool(Mode, "E") && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && (Math.Abs(Player.PercentCooldownMod) < 0.2 || (Math.Abs(Player.PercentCooldownMod) >= 0.2 && Player.LastCastedSpellName() == "Overload"))) SkillE.CastOnUnit(targetObj, PacketCast());
                    }
                    break;
                case "Chase":
                    if (SkillW.IsReady() && SkillW.InRange(targetObj.Position)) SkillW.CastOnUnit(targetObj, PacketCast());
                    if (!SkillW.IsReady() || targetObj.HasBuff("Rune Prison"))
                    {
                        if (SkillQ.IsReady() && SkillQ.InRange(targetObj.Position)) SkillQ.CastOnUnit(targetObj, PacketCast());
                        if (SkillR.IsReady() && (Math.Abs(Player.PercentCooldownMod) < 0.2 || (Math.Abs(Player.PercentCooldownMod) >= 0.2 && Player.LastCastedSpellName() == "Overload")) && (Player.HealthPercentage() <= 40 || Player.CountEnemysInRange((int)SkillQ.Range + 200) == 1 || Player.CountEnemysInRange((int)SkillQ.Range + 300) >= 2)) SkillR.Cast(PacketCast());
                        if (!SkillR.IsReady() && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && (Math.Abs(Player.PercentCooldownMod) < 0.2 || (Math.Abs(Player.PercentCooldownMod) >= 0.2 && (Player.LastCastedSpellName() == "Overload" || (!SkillR.IsReady() && Player.LastCastedSpellName() == "DesperatePower" && Player.HasBuff("DesperatePower")))))) SkillE.CastOnUnit(targetObj, PacketCast());
                    }
                    break;
            }
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, SkillQ.Range)).OrderBy(i => i.Health))
            {
                if (ItemBool("Clear", "Q") && SkillQ.IsReady() && (CanKill(Obj, SkillQ) || Obj.MaxHealth >= 1200 || SkillQ.GetHealthPrediction(Obj) + 5 > SkillQ.GetDamage(Obj) * 2)) SkillQ.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "W") && SkillW.IsReady() && SkillW.InRange(Obj.Position) && (CanKill(Obj, SkillW) || Obj.MaxHealth >= 1200 || SkillW.GetHealthPrediction(Obj) + 5 > SkillW.GetDamage(Obj) * 2)) SkillW.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "E") && SkillE.IsReady() && SkillE.InRange(Obj.Position) && (CanKill(Obj, SkillE) || Obj.MaxHealth >= 1200 || SkillE.GetHealthPrediction(Obj) + 5 > SkillE.GetDamage(Obj) * 2)) SkillE.CastOnUnit(Obj, PacketCast());
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !SkillQ.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, (ItemBool("Misc", "Exploit") && SkillW.IsReady()) ? SkillW.Range : SkillQ.Range) && CanKill(i, SkillQ)).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player)))
            {
                SkillQ.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Misc", "Exploit") && SkillW.IsReady()) Utility.DelayAction.Add((int)(Player.Distance3D(Obj) / SkillQ.Speed * 1000 - 400), () => SkillW.CastOnUnit(Obj, PacketCast()));
            }
        }

        private void KillSteal()
        {
            if (!SkillQ.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, SkillQ.Range) && CanKill(i, SkillQ) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) SkillQ.CastOnUnit(Obj, PacketCast());
        }
    }
}