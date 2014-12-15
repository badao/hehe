using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class JarvanIV : Program
    {
        private bool RCasted = false;
        private Vector3 FlagPos = default(Vector3);

        public JarvanIV()
        {
            Q = new Spell(SpellSlot.Q, 770);
            W = new Spell(SpellSlot.W, 525);
            E = new Spell(SpellSlot.E, 860);
            R = new Spell(SpellSlot.R, 650);
            Q.SetSkillshot(-0.5f, 70, 20, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(-0.5f, 0, 1450, false, SkillshotType.SkillshotCircle);
            R.SetTargetted(-0.5f, 0);

            Config.SubMenu("OW").SubMenu("Mode").AddItem(new MenuItem("OWEQFlash", "EQ Flash", true).SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));
            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemSlider(ComboMenu, "WUnder", "-> If Hp Under", 20);
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemList(ComboMenu, "RMode", "-> Mode", new[] { "Killable", "# Enemy" });
                    ItemSlider(ComboMenu, "RAbove", "--> If Enemy Above", 2, 1, 4);
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemSlider(HarassMenu, "QAbove", "-> To Flag If Hp Above", 20);
                    ItemBool(HarassMenu, "E", "Use E");
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
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra Item");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "QLastHit", "Use Q To Last Hit");
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemBool(MiscMenu, "EQInterrupt", "Use EQ To Interrupt");
                    ItemBool(MiscMenu, "WSurvive", "Try Use W To Survive");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 5, 0, 6).ValueChanged += SkinChanger;
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
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Obj_AI_Base.OnCreate += OnCreateBase;
            Obj_AI_Base.OnDelete += OnDeleteBase;
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
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            if (ItemActive("EQFlash")) EQFlash();
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "W") && W.Level > 0) Utility.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "R") && R.Level > 0) Utility.DrawCircle(Player.Position, R.Range, R.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "EQInterrupt") || !Q.IsReady()) return;
            if (IsValid(unit, Q.Range) && E.IsReady() && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost) E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 150), PacketCast());
            if (FlagPos != default(Vector3) && (FlagPos.Distance(unit.Position) <= 60 || Q.WillHit(unit.Position, FlagPos, 110))) Q.Cast(FlagPos, PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Player.IsDead) return;
            if (sender.IsMe)
            {
                if (args.SData.Name == "JarvanIVCataclysm")
                {
                    RCasted = true;
                    Utility.DelayAction.Add(3500, () => RCasted = false);
                }
            }
            else if (sender.IsEnemy && ItemBool("Misc", "WSurvive") && W.IsReady())
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

        private void OnCreateBase(GameObject sender, EventArgs args)
        {
            if (FlagPos == default(Vector3) && sender.Name == "JarvanDemacianStandard_buf_green.troy") FlagPos = sender.Position;
        }

        private void OnDeleteBase(GameObject sender, EventArgs args)
        {
            if (FlagPos != default(Vector3) && sender.Name == "JarvanDemacianStandard_buf_green.troy") FlagPos = default(Vector3);
        }

        private void NormalCombo(string Mode)
        {
            if (Mode == "Combo" && ItemBool(Mode, "R") && ItemList(Mode, "RMode") == 0 && R.IsReady() && RCasted && Player.CountEnemysInRange(325) == 0) R.Cast(PacketCast());
            if (targetObj == null) return;
            if (ItemBool(Mode, "E") && E.IsReady() && E.InRange(targetObj.Position)) E.Cast((Player.Distance3D(targetObj) > 450 && !targetObj.IsFacing(Player)) ? Player.Position.To2D().Extend(targetObj.Position.To2D(), targetObj.Distance3D(Player) + 150) : targetObj.Position.To2D(), PacketCast());
            if ((!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && !E.IsReady())) && ItemBool(Mode, "Q") && Q.IsReady())
            {
                if (ItemBool(Mode, "E") && FlagPos != default(Vector3))
                {
                    if ((FlagPos.Distance(targetObj.Position) <= 60 || Q.WillHit(targetObj.Position, FlagPos, 110)) && Q.InRange(FlagPos))
                    {
                        if (Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= ItemSlider(Mode, "QAbove"))) Q.Cast(FlagPos, PacketCast());
                    }
                    else if (Q.InRange(targetObj.Position)) Q.Cast(targetObj.Position, PacketCast());
                }
                else if ((!ItemBool(Mode, "E") || (ItemBool(Mode, "E") && FlagPos == default(Vector3))) && Q.InRange(targetObj.Position)) Q.Cast(targetObj.Position, PacketCast());
            }
            if (Mode == "Combo" && ItemBool(Mode, "R") && R.IsReady())
            {
                if (!RCasted)
                {
                    switch (ItemList(Mode, "RMode"))
                    {
                        case 0:
                            if (R.InRange(targetObj.Position) && CanKill(targetObj, R)) R.CastOnUnit(targetObj, PacketCast());
                            break;
                        case 1:
                            var UltiObj = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => IsValid(i, R.Range) && i.CountEnemysInRange(325) >= ItemSlider(Mode, "RAbove"));
                            if (UltiObj != null) R.CastOnUnit(UltiObj, PacketCast());
                            break;
                    }
                }
                else if (Player.CountEnemysInRange(325) == 0) R.Cast(PacketCast());
            }
            if (Mode == "Combo" && ItemBool(Mode, "W") && W.IsReady() && W.InRange(targetObj.Position) && Player.HealthPercentage() <= ItemSlider(Mode, "WUnder")) W.Cast(PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = ObjectManager.Get<Obj_AI_Base>().Where(i => IsValid(i, E.Range) && i is Obj_AI_Minion).OrderBy(i => i.Health);
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady())
                {
                    var posEFarm1 = E.GetCircularFarmLocation(minionObj.Where(i => !i.IsMelee()).ToList());
                    var posEFarm2 = E.GetCircularFarmLocation(minionObj.ToList());
                    if (posEFarm1.MinionsHit >= 3)
                    {
                        E.Cast(posEFarm1.Position, PacketCast());
                    }
                    else E.Cast(posEFarm2.MinionsHit >= 2 ? posEFarm2.Position : Obj.Position.To2D(), PacketCast());
                }
                if (ItemBool("Clear", "Q") && Q.IsReady())
                {
                    var posQFarm1 = Q.GetLineFarmLocation(minionObj.Where(i => IsValid(i, Q.Range) && !i.IsMelee()).ToList());
                    var posQFarm2 = Q.GetLineFarmLocation(minionObj.Where(i => IsValid(i, Q.Range)).ToList());
                    if (Q.InRange(Obj.Position) && CanKill(Obj, Q))
                    {
                        Q.Cast(Obj.Position, PacketCast());
                    }
                    else if (posQFarm1.MinionsHit >= 3)
                    {
                        Q.Cast(posQFarm1.Position, PacketCast());
                    }
                    else if (posQFarm2.MinionsHit >= 2)
                    {
                        Q.Cast(posQFarm2.Position, PacketCast());
                    }
                    else if (Q.InRange(Obj.Position)) Q.Cast(Obj, PacketCast());
                }
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "QLastHit") || !Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Minion>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q)).OrderBy(i => i.Health).OrderByDescending(i => i.Distance3D(Player))) Q.Cast(Obj.Position, PacketCast());
        }

        private void Flee()
        {
            if (!Q.IsReady()) return;
            if (E.IsReady() && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost) E.Cast(Game.CursorPos, PacketCast());
            Q.Cast(Game.CursorPos, PacketCast());
        }

        private void EQFlash()
        {
            CustomOrbwalk(targetObj);
            if (targetObj == null || !Q.IsReady()) return;
            if (E.IsReady() && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost) E.Cast(Player.Position.To2D().Extend(targetObj.Position.To2D(), (!Q.InRange(targetObj.Position) && Player.Distance3D(targetObj) <= Q.Range + 370 && FlashReady()) ? Q.Range : targetObj.Distance3D(Player) + 150), PacketCast());
            if (FlagPos != default(Vector3) && Q.InRange(FlagPos) && (FlagPos.Distance(targetObj.Position) <= 60 || Q.WillHit(targetObj.Position, FlagPos, 110) || (FlashReady() && Player.Distance3D(targetObj) <= Q.Range + 370)))
            {
                Q.Cast(FlagPos, PacketCast());
                if (FlashReady() && Player.LastCastedSpellName() == "JarvanIVDragonStrike" && (FlagPos.Distance(targetObj.Position) > 60 || !Q.WillHit(targetObj.Position, FlagPos, 110)) && Player.Distance3D(targetObj) <= Q.Range + 370) Utility.DelayAction.Add((int)((Player.Distance3D(targetObj) - Q.Range) / E.Speed * 1000 + 500), () => CastFlash(targetObj.Position));
            }
        }

        private void KillSteal()
        {
            if (!Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => IsValid(i, Q.Range) && CanKill(i, Q) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player))) Q.Cast(Obj.Position, PacketCast());
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
        }
    }
}