using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Lucian : Program
    {
        private bool QCasted = false, WCasted = false, ECasted = false, WillInAA = false;
        private Spell Q2;
        private Vector2 REndPos = default(Vector2);

        public Lucian()
        {
            Q = new Spell(SpellSlot.Q, 675);
            Q2 = new Spell(SpellSlot.Q, 1100);
            W = new Spell(SpellSlot.W, 1000);
            E = new Spell(SpellSlot.E, 445);
            R = new Spell(SpellSlot.R, 1400);
            Q.SetTargetted(0, 500);
            Q2.SetSkillshot(0, 65, 500, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(0, 80, 500, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(-0.7f, 50, 500, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(0, 60, 500, true, SkillshotType.SkillshotLine);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Passive", "Use Passive");
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemSlider(ComboMenu, "EDelay", "-> Stop All If E Will Ready In (ms)", 2000, 0, 4000);
                    ItemBool(ComboMenu, "R", "Use R If Killable");
                    ItemBool(ComboMenu, "CancelR", "-> Stop R For Kill Steal");
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Passive", "Use Passive");
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ItemSlider(HarassMenu, "EAbove", "-> If Hp Above", 20);
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemSlider(ClearMenu, "EDelay", "-> Stop All If E Will Ready In (ms)", 2000, 0, 4000);
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 2, 0, 2).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
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
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsRecalling()) return;
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
            if (Player.IsChannelingImportantSpell())
            {
                if (ItemBool("Combo", "R"))
                {
                    if (Player.CountEnemysInRange((int)R.Range + 60) == 0) R.Cast(PacketCast());
                    if (targetObj != null) LockROnTarget(targetObj);
                }
                return;
            }
            else REndPos = default(Vector2);
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) LaneJungClear();
            if (Orbwalk.CurrentMode != Orbwalk.Mode.Combo) WillInAA = false;
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsMe)
            {
                if (args.SData.Name == "LucianQ")
                {
                    QCasted = true;
                    Utility.DelayAction.Add(250, () => QCasted = false);
                }
                if (args.SData.Name == "LucianW")
                {
                    WCasted = true;
                    Utility.DelayAction.Add(350, () => WCasted = false);
                }
                if (args.SData.Name == "LucianE")
                {
                    ECasted = true;
                    Utility.DelayAction.Add(250, () => ECasted = false);
                }
            }
        }

        private void AfterAttack(Obj_AI_Base Unit, Obj_AI_Base Target)
        {
            if (!Unit.IsMe) return;
            if ((((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) && ItemBool("Clear", "E") && !HavePassive()) || ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || (Orbwalk.CurrentMode == Orbwalk.Mode.Harass && Player.HealthPercentage() >= ItemSlider(Orbwalk.CurrentMode.ToString(), "EAbove"))) && ItemBool(Orbwalk.CurrentMode.ToString(), "E") && !HavePassive(Orbwalk.CurrentMode.ToString()) && Target is Obj_AI_Hero)) && E.IsReady())
            {
                if (Player.Position.Distance(Game.CursorPos) > 100 && Target.Distance3D(Player) <= E.Range + Orbwalk.GetAutoAttackRange(Player, Target) /*&& Target.Position.Distance(Player.Position.To2D().Extend(Game.CursorPos.To2D(), E.Range).To3D()) <= Orbwalk.GetAutoAttackRange(Player, Target)*/)
                {
                    E.Cast(Player.Position.To2D().Extend(Game.CursorPos.To2D(), E.Range), PacketCast());
                }
                //var Pos = (Player.Position.Distance(Game.CursorPos) <= E.Range && Player.Position.Distance(Game.CursorPos) > 100) ? Game.CursorPos : Player.Position.To2D().Extend(Game.CursorPos.To2D(), E.Range).To3D();
                //if (Target.Position.Distance(Pos) <= Orbwalk.GetAutoAttackRange(Player, Target))
                //{
                //    if ((Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) && ObjectManager.Get<Obj_AI_Minion>().Count(i => IsValid(i, Orbwalk.GetAutoAttackRange() + E.Range)) == 0) return;
                //    if ((Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass) && ObjectManager.Get<Obj_AI_Hero>().Count(i => IsValid(i, Orbwalk.GetAutoAttackRange() + E.Range)) == 0) return;
                //    E.Cast(Pos, PacketCast());
                //    if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo) WillInAA = true;
                //}
                //WillInAA = false;
            }
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "Q") && Q.IsReady() && !Player.IsDashing() && CanKill(targetObj, Q))
            {
                if (Q.InRange(targetObj.Position))
                {
                    Q.CastOnUnit(targetObj, PacketCast());
                }
                else if (Q2.InRange(targetObj.Position))
                {
                    foreach (var Obj in Q2.GetPrediction(targetObj).CollisionObjects.Where(i => IsValid(i, Q.Range) && Q2.WillHit(i.Position, targetObj.Position))) Q.CastOnUnit(Obj, PacketCast());
                }
            }
            if (ItemBool(Mode, "W") && W.IsReady() && W.InRange(targetObj.Position) && !Player.IsDashing() && CanKill(targetObj, W)) W.Cast(targetObj.Position, PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "R") && R.IsReady() && R.InRange(targetObj.Position) && !Player.IsDashing() && R.GetHealthPrediction(targetObj) + 5 <= GetRDmg(targetObj))
            {
                if (Player.Distance3D(targetObj) > 500 && Player.Distance3D(targetObj) <= 800 && (!ItemBool(Mode, "Q") || (ItemBool(Mode, "Q") && !Q.IsReady())) && (!ItemBool(Mode, "W") || (ItemBool(Mode, "W") && !W.IsReady())) && (!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && !E.IsReady())))
                {
                    R.Cast(targetObj.Position, PacketCast());
                    REndPos = (Player.Position.To2D() - targetObj.Position.To2D()).Normalized();
                }
                else if (Player.Distance3D(targetObj) > 800 && Player.Distance3D(targetObj) <= 1075)
                {
                    R.Cast(targetObj.Position, PacketCast());
                    REndPos = (Player.Position.To2D() - targetObj.Position.To2D()).Normalized();
                }
            }
            if (Mode == "Combo" && ItemBool(Mode, "E") && E.IsReady() && !Orbwalk.InAutoAttackRange(targetObj) && targetObj.Position.Distance(Player.Position.To2D().Extend(Game.CursorPos.To2D(), E.Range).To3D()) + 30 <= Orbwalk.GetAutoAttackRange(Player, targetObj)) E.Cast(Game.CursorPos, PacketCast());
            if (!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && (!E.IsReady() /*|| (Mode == "Combo" && E.IsReady() && !WillInAA)*/)))
            {
                if (Mode == "Combo" && ItemBool(Mode, "E") && E.IsReady(ItemSlider(Mode, "EDelay"))) return;
                if (ItemBool(Mode, "Q") && Q.IsReady() && !Player.IsDashing())
                {
                    if ((Orbwalk.InAutoAttackRange(targetObj) && !HavePassive(Mode)) || (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 50 && Q.InRange(targetObj.Position)))
                    {
                        Q.CastOnUnit(targetObj, PacketCast());
                    }
                    else if (!Q.InRange(targetObj.Position) && Q2.InRange(targetObj.Position))
                    {
                        foreach (var Obj in Q2.GetPrediction(targetObj).CollisionObjects.Where(i => IsValid(i, Q.Range) && Q2.WillHit(i.Position, targetObj.Position))) Q.CastOnUnit(Obj, PacketCast());
                    }
                }
                if ((!ItemBool(Mode, "Q") || (ItemBool(Mode, "Q") && !Q.IsReady())) && ItemBool(Mode, "W") && W.IsReady() && !Player.IsDashing() && ((Orbwalk.InAutoAttackRange(targetObj) && !HavePassive(Mode)) || (Player.Distance3D(targetObj) > Orbwalk.GetAutoAttackRange(Player, targetObj) + 50 && W.InRange(targetObj.Position)))) W.Cast(targetObj.Position, PacketCast());
            }
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q2.Range)).OrderBy(i => i.Health);
            foreach (var Obj in minionObj)
            {
                if (!ItemBool("Clear", "E") || (ItemBool("Clear", "E") && !E.IsReady()))
                {
                    if (ItemBool("Clear", "E") && E.IsReady(ItemSlider("Clear", "EDelay"))) return;
                    if (ItemBool("Clear", "W") && W.IsReady() && !Player.IsDashing() && !HavePassive())
                    {
                        if (W.InRange(Obj.Position) && Obj.Team == GameObjectTeam.Neutral && Obj.MaxHealth >= 1200)
                        {
                            W.CastIfHitchanceEquals(Obj, HitChance.Medium, PacketCast());
                        }
                        else
                        {
                            var BestW = 0;
                            var BestWPos = default(Vector3);
                            foreach (var Sub in minionObj.Where(i => W.InRange(i.Position) && W.GetPrediction(i).Hitchance >= HitChance.Low))
                            {
                                var Hit = minionObj.Count(i => i.Distance3D(Sub) <= W.Width);
                                if (Hit > BestW || BestWPos == default(Vector3))
                                {
                                    BestW = Hit;
                                    BestWPos = Sub.Position;
                                }
                            }
                            if (BestWPos != default(Vector3)) W.Cast(BestWPos, PacketCast());
                        }
                    }
                    if ((!ItemBool("Clear", "W") || (ItemBool("Clear", "W") && !W.IsReady())) && ItemBool("Clear", "Q") && Q.IsReady() && Q.InRange(Obj.Position) && !Player.IsDashing() && !HavePassive() && (minionObj.Count(i => Q2.WillHit(Obj.Position, i.Position)) >= 2 || CanKill(Obj, Q) || Obj.MaxHealth >= 1200)) Q.CastOnUnit(Obj, PacketCast());
                }
            }
        }

        private void KillSteal()
        {
            if (!Q.IsReady() || ((!ItemBool("Combo", "R") || (ItemBool("Combo", "R") && !ItemBool("Combo", "CancelR"))) && Player.IsChannelingImportantSpell())) return;
            var CancelR = ItemBool("Combo", "R") && ItemBool("Combo", "CancelR") && Player.IsChannelingImportantSpell();
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, Q2.Range) && CanKill(i, Q) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player)))
            {
                if (Q.InRange(Obj.Position))
                {
                    if (CancelR) R.Cast(PacketCast());
                    Q.CastOnUnit(Obj, PacketCast());
                }
                else
                {
                    foreach (var Collision in Q2.GetPrediction(Obj).CollisionObjects.Where(i => IsValid(i, Q.Range) && Q2.WillHit(i.Position, Obj.Position)))
                    {
                        if (CancelR) R.Cast(PacketCast());
                        Q.CastOnUnit(Collision, PacketCast());
                    }
                }
            }
        }

        private void UseItem(Obj_AI_Base Target)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Youmuu) && Player.CountEnemysInRange(480) >= 1) Items.UseItem(Youmuu);
        }

        private bool HavePassive(string Mode = "Clear")
        {
            if (Mode != "Clear" && !ItemBool(Mode, "Passive")) return false;
            if (QCasted || WCasted || ECasted || Player.HasBuff("LucianPassiveBuff")) return true;
            return false;
        }

        private double GetRDmg(Obj_AI_Hero Target)
        {
            var Shot = (int)(7.5 + new double[] { 7.5, 9, 10.5 }[R.Level - 1] * 1 / Player.AttackDelay);
            var MaxShot = new int[] { 26, 30, 33 }[R.Level - 1];
            return Player.CalcDamage(Target, Damage.DamageType.Physical, (new double[] { 40, 50, 60 }[R.Level - 1] + 0.25 * Player.FlatPhysicalDamageMod + 0.1 * Player.FlatMagicDamageMod) * (Shot > MaxShot ? MaxShot : Shot));
        }

        private void LockROnTarget(Obj_AI_Hero Target)
        {
            var PredR = R.GetPrediction(Target).CastPosition.To2D();
            var Pos = new Vector2((float)(PredR.X + REndPos.X * R.Range * 0.98), (float)(PredR.Y + REndPos.Y * R.Range * 0.98));
            if (PredR.IsValid() && Pos.IsValid() && !Utility.IsWall(Pos.To3D()) && PredR.Distance(Pos) <= R.Range && PredR.Distance(Pos) > 100)
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, Pos.To3D());
            }
            else Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
        }
    }
}