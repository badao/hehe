using System;
using System.Linq;
using System.Collections.Generic;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Shen : Program
    {
        private bool PingCasted = false;

        public Shen()
        {
            Q = new Spell(SpellSlot.Q, 475);
            W = new Spell(SpellSlot.W, 20);
            E = new Spell(SpellSlot.E, 600);
            R = new Spell(SpellSlot.R, 25000);
            Q.SetTargetted(-0.5f, 1500);
            E.SetSkillshot(-0.5f, 50, 0, false, SkillshotType.SkillshotLine);

            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWFlashTaunt", "Flash Taunt", true).SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));
            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemSlider(ComboMenu, "WUnder", "-> If Hp Under", 20);
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemSlider(HarassMenu, "EAbove", "-> If Hp Above", 20);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var UltiMenu = new Menu("Ultimate", "Ultimate");
                {
                    ItemBool(UltiMenu, "Alert", "Alert Ally Low Hp");
                    ItemSlider(UltiMenu, "HpUnder", "-> If Hp Under", 30);
                    ItemBool(UltiMenu, "Ping", "-> Ping Fallback");
                    ChampMenu.AddSubMenu(UltiMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ItemBool(MiscMenu, "EAntiGap", "Use E To Anti Gap Closer");
                    ItemBool(MiscMenu, "EInterrupt", "Use E To Interrupt");
                    ItemBool(MiscMenu, "EUnderTower", "Use E If Enemy Under Tower");
                    ItemBool(MiscMenu, "WSurvive", "Try Use W To Survive");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 6, 0, 6).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
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
        }

        private void OnGameUpdate(EventArgs args)
        {
            //Passive: Shen Passive Aura
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee && E.IsReady()) E.Cast(Game.CursorPos, PacketCast());
            if (ItemActive("FlashTaunt")) FlashTaunt();
            if (ItemBool("Ultimate", "Alert")) UltimateAlert();
            if (ItemBool("Misc", "EUnderTower")) AutoEUnderTower();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "EAntiGap") || Player.IsDead || !E.IsReady()) return;
            if (IsValid(gapcloser.Sender, Orbwalk.GetAutoAttackRange() + 100)) E.Cast(Player.Position.To2D().Extend(gapcloser.Sender.Position.To2D(), gapcloser.Sender.Distance3D(Player) + 200), PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "EInterrupt") || Player.IsDead || !E.IsReady()) return;
            if (IsValid(unit, E.Range)) E.Cast(Player.Position.To2D().Extend(unit.Position.To2D(), unit.Distance3D(Player) + 200), PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsEnemy && ItemBool("Misc", "WSurvive") && W.IsReady())
            {
                if (args.Target.IsMe && ((Orbwalk.IsAutoAttack(args.SData.Name) && Player.Health <= sender.GetAutoAttackDamage(Player, true)) || (args.SData.Name == "summonerdot" && Player.Health <= (sender as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite))))
                {
                    W.Cast(PacketCast());
                }
                else if ((args.Target.IsMe || (Player.Position.Distance(args.Start) <= args.SData.CastRange[0] && Player.Position.Distance(args.End) <= Orbwalk.GetAutoAttackRange())) && Damage.Spells.ContainsKey((sender as Obj_AI_Hero).ChampionName))
                {
                    for (var i = 3; i > -1; i--)
                    {
                        if (Damage.Spells[(sender as Obj_AI_Hero).ChampionName].FirstOrDefault(a => a.Slot == (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false) && a.Stage == i) != null)
                        {
                            if (Player.Health <= (sender as Obj_AI_Hero).GetSpellDamage(Player, (sender as Obj_AI_Hero).GetSpellSlot(args.SData.Name, false), i)) W.Cast(PacketCast());
                        }
                    }
                }
            }
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (Mode == "Combo" && ItemBool(Mode, "Item"))
            {
                if (Items.CanUseItem(Deathfire) && Player.Distance3D(targetObj) <= 750) Items.UseItem(Deathfire, targetObj);
                if (Items.CanUseItem(Blackfire) && Player.Distance3D(targetObj) <= 750) Items.UseItem(Blackfire, targetObj);
                if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Randuin);
            }
            if (ItemBool(Mode, "E") && E.IsReady() && E.InRange(targetObj.Position) && (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= ItemSlider(Mode, "EAbove")))) E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 200), PacketCast());
            if (ItemBool(Mode, "Q") && Q.IsReady() && Q.InRange(targetObj.Position)) Q.CastOnUnit(targetObj, PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "W") && W.IsReady() && Orbwalk.InAutoAttackRange(targetObj) && Player.HealthPercentage() <= ItemSlider(Mode, "WUnder")) W.Cast(PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range)).OrderBy(i => i.Health))
            {
                if (ItemBool("Clear", "Q") && Q.IsReady()) Q.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "W") && W.IsReady() && Orbwalk.InAutoAttackRange(Obj)) W.Cast(PacketCast());
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q)).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player))) Q.CastOnUnit(Obj, PacketCast());
        }

        private void FlashTaunt()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null || !E.IsReady()) return;
            if (E.InRange(targetObj.Position))
            {
                E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 200), PacketCast());
            }
            else if (Player.Distance3D(targetObj) <= E.Range + 390 && FlashReady()) CastFlash(targetObj.Position);
        }

        private void UltimateAlert()
        {
            if (!R.IsReady() || PingCasted) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, float.MaxValue, false) && i.CountEnemysInRange(800) >= 1 && i.HealthPercentage() <= ItemSlider("Ultimate", "HpUnder")))
            {
                Game.PrintChat("<font color = \'{0}'>-></font> <font color = \'{1}'>{2}</font>: <font color = \'{3}'>In Dangerous</font>", HtmlColor.BlueViolet, HtmlColor.Gold, Obj.ChampionName, HtmlColor.Cyan);
                if (ItemBool("Ultimate", "Ping"))
                {
                    for (var i = 0; i < 3; i++) Packet.S2C.Ping.Encoded(new Packet.S2C.Ping.Struct(Obj.Position.X, Obj.Position.Y, Obj.NetworkId, 0, Packet.PingType.Fallback)).Process();
                    PingCasted = true;
                    Utility.DelayAction.Add(5000, () => PingCasted = false);
                }
            }
        }

        private void AutoEUnderTower()
        {
            if (Player.UnderTurret() || !E.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, E.Range)).OrderBy(i => i.Distance3D(Player)))
            {
                var TowerObj = ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(i => IsValid(i, 950, false));
                if (TowerObj != null && Obj.Distance3D(TowerObj) <= 950) E.Cast(Player.Position.To2D().Extend(Obj.Position.To2D(), Obj.Distance3D(Player) + 200), PacketCast());
            }
        }
    }
}