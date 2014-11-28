using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class Jax : Program
    {
        private Int32 Sheen = 3057, Trinity = 3078;
        private Int32 RCount = 0;
        private bool WardCasted = false;

        public Jax()
        {
            SkillQ = new Spell(SpellSlot.Q, 700);
            SkillW = new Spell(SpellSlot.W, 300);
            SkillE = new Spell(SpellSlot.E, 187.5f);
            SkillR = new Spell(SpellSlot.R, 100);
            SkillQ.SetTargetted(SkillQ.Instance.SData.SpellCastTime, SkillQ.Instance.SData.MissileSpeed);

            Config.AddSubMenu(new Menu("Combo", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wuseMode", "W Mode").SetValue(new StringList(new[] { "After AA", "After R" })));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("rusage", "Use R").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("ruseMode", "R Mode").SetValue(new StringList(new[] { "Player Hp", "Count Enemy" })));
            Config.SubMenu("csettings").AddItem(new MenuItem("ruseHp", "Use R If Hp Under").SetValue(new Slider(50, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem("ruseEnemy", "Use R If Enemy Above").SetValue(new Slider(2, 1, 4)));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Harass", "hsettings"));
            Config.SubMenu("hsettings").AddItem(new MenuItem("useHarQ", "Use Q").SetValue(true));
            Config.SubMenu("hsettings").AddItem(new MenuItem("harMode", "Use Q If Hp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("hsettings").AddItem(new MenuItem("useHarW", "Use W").SetValue(true));
            Config.SubMenu("hsettings").AddItem(new MenuItem("useHarE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearW", "Use W").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearWMode", "W Mode").SetValue(new StringList(new[] { "After AA", "After R" })));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("lasthitW", "Use W To Last Hit").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("killstealWQ", "Auto WQ To Kill Steal").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("useAntiE", "Use E To Anti Gap Closer").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("useInterE", "Use E To Interrupt").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("CustomSkin", "Skin Changer").SetValue(new Slider(8, 0, 8))).ValueChanged += SkinChanger;

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawQ", "Q Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawE", "E Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen)
            {
                if (Player.IsDead) RCount = 0;
                return;
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
                    if (Config.Item("lasthitW").GetValue<bool>()) LastHit();
                    break;
                case Orbwalk.Mode.Flee:
                    WardJump(Game.CursorPos);
                    break;
            }
            if (Config.Item("killstealWQ").GetValue<bool>()) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item("DrawQ").GetValue<bool>() && SkillQ.Level > 0) Utility.DrawCircle(Player.Position, SkillQ.Range, SkillQ.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!Config.Item("useAntiE").GetValue<bool>()) return;
            if (gapcloser.Sender.IsValidTarget(SkillE.Range) && SkillE.IsReady()) SkillE.Cast(PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!Config.Item("useInterE").GetValue<bool>()) return;
            if (SkillQ.IsReady() && SkillE.IsReady() && !SkillE.InRange(unit.Position) && unit.IsValidTarget(SkillQ.Range)) SkillQ.CastOnUnit(unit, PacketCast());
            if (unit.IsValidTarget(SkillE.Range) && SkillE.IsReady()) SkillE.Cast(PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == "jaxrelentlessattack")
            {
                RCount = 0;
                if (SkillW.IsReady() && (args.Target as Obj_AI_Base).IsValidTarget(Orbwalk.GetAutoAttackRange(Player, (Obj_AI_Base)args.Target) + 50))
                {
                    switch (Orbwalk.CurrentMode)
                    {
                        case Orbwalk.Mode.Combo:
                            if (Config.Item("wusage").GetValue<bool>() && Config.Item("wuseMode").GetValue<StringList>().SelectedIndex == 1) SkillW.Cast(PacketCast());
                            break;
                        case Orbwalk.Mode.LaneClear:
                            if (Config.Item("useClearW").GetValue<bool>() && Config.Item("useClearWMode").GetValue<StringList>().SelectedIndex == 1) SkillW.Cast(PacketCast());
                            break;
                        case Orbwalk.Mode.LaneFreeze:
                            if (Config.Item("useClearW").GetValue<bool>() && Config.Item("useClearWMode").GetValue<StringList>().SelectedIndex == 1) SkillW.Cast(PacketCast());
                            break;
                    }
                }
            }
        }

        private void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (!unit.IsMe) return;
            if (SkillR.Level > 0) RCount += 1;
            if (SkillW.IsReady() && target.IsValidTarget(Orbwalk.GetAutoAttackRange(Player, target) + 50))
            {
                switch (Orbwalk.CurrentMode)
                {
                    case Orbwalk.Mode.Combo:
                        if (Config.Item("wusage").GetValue<bool>() && Config.Item("wuseMode").GetValue<StringList>().SelectedIndex == 0) SkillW.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.Harass:
                        if (Config.Item("useHarW").GetValue<bool>() && (!Config.Item("useHarQ").GetValue<bool>() || (Config.Item("useHarQ").GetValue<bool>()) && !SkillQ.IsReady())) SkillW.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.LaneClear:
                        if (Config.Item("useClearW").GetValue<bool>() && Config.Item("useClearWMode").GetValue<StringList>().SelectedIndex == 0) SkillW.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.LaneFreeze:
                        if (Config.Item("useClearW").GetValue<bool>() && Config.Item("useClearWMode").GetValue<StringList>().SelectedIndex == 0) SkillW.Cast(PacketCast());
                        break;
                }
            }
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item("eusage").GetValue<bool>() && SkillE.IsReady())
            {
                if (!Player.HasBuff("JaxCounterStrike", true))
                {
                    if ((Config.Item("qusage").GetValue<bool>() && SkillQ.InRange(targetObj.Position) && SkillQ.IsReady()) || SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast());
                }
                else if (SkillE.InRange(targetObj.Position) && !targetObj.IsValidTarget(SkillE.Range - 3.5f)) SkillE.Cast(PacketCast());
            }
            if (Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady() && SkillQ.InRange(targetObj.Position))
            {
                if ((Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && Player.HasBuff("JaxCounterStrike", true) && !SkillE.InRange(targetObj.Position)) || (!Orbwalk.InAutoAttackRange(targetObj) && Player.Distance(targetObj) > 450)) SkillQ.CastOnUnit(targetObj, PacketCast());
            }
            if (Config.Item("rusage").GetValue<bool>() && SkillR.IsReady())
            {
                switch (Config.Item("ruseMode").GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        if (Player.Health * 100 / Player.MaxHealth < Config.Item("ruseHp").GetValue<Slider>().Value) SkillR.Cast(PacketCast());
                        break;
                    case 1:
                        if (Player.CountEnemysInRange((int)SkillQ.Range) >= Config.Item("ruseEnemy").GetValue<Slider>().Value) SkillR.Cast(PacketCast());
                        break;
                }
            }
            if (Config.Item("iusage").GetValue<bool>()) UseItem(targetObj);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void Harass()
        {
            if (targetObj == null) return;
            if (Config.Item("useHarW").GetValue<bool>() && SkillW.IsReady())
            {
                if (Config.Item("useHarQ").GetValue<bool>() && SkillQ.InRange(targetObj.Position) && SkillQ.IsReady()) SkillW.Cast(PacketCast());
            }
            if (Config.Item("useHarE").GetValue<bool>() && SkillE.IsReady())
            {
                if (!Player.HasBuff("JaxCounterStrike", true))
                {
                    if ((Config.Item("useHarQ").GetValue<bool>() && SkillQ.InRange(targetObj.Position) && SkillQ.IsReady()) || SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast());
                }
                else if (SkillE.InRange(targetObj.Position) && !targetObj.IsValidTarget(SkillE.Range - 3.5f)) SkillE.Cast(PacketCast());
            }
            if (Config.Item("useHarQ").GetValue<bool>() && SkillQ.IsReady() && SkillQ.InRange(targetObj.Position) && Player.Health * 100 / Player.MaxHealth >= Config.Item("harMode").GetValue<Slider>().Value)
            {
                if ((Config.Item("useHarE").GetValue<bool>() && SkillE.IsReady() && Player.HasBuff("JaxCounterStrike", true) && !SkillE.InRange(targetObj.Position)) || (!Orbwalk.InAutoAttackRange(targetObj) && Player.Distance(targetObj) > 450)) SkillQ.CastOnUnit(targetObj, PacketCast());
            }
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillQ.Range, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null) return;
            if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady())
            {
                if (!Player.HasBuff("JaxCounterStrike", true))
                {
                    if ((Config.Item("useClearQ").GetValue<bool>() && SkillQ.InRange(minionObj.Position) && SkillQ.IsReady()) || SkillE.InRange(minionObj.Position)) SkillE.Cast(PacketCast());
                }
                else if (SkillE.InRange(minionObj.Position) && !minionObj.IsValidTarget(SkillE.Range - 3.5f)) SkillE.Cast(PacketCast());
            }
            if (Config.Item("useClearQ").GetValue<bool>() && SkillQ.IsReady() && SkillQ.InRange(minionObj.Position))
            {
                if ((Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady() && Player.HasBuff("JaxCounterStrike", true) && !SkillE.InRange(minionObj.Position)) || (!Orbwalk.InAutoAttackRange(minionObj) && Player.Distance(minionObj) > 450)) SkillQ.CastOnUnit(minionObj, PacketCast());
            }
        }

        private void LastHit()
        {
            var minionObj = (Obj_AI_Base)ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(i => i.IsValidTarget(Orbwalk.GetAutoAttackRange(Player, i) + 50) && i.Health <= GetBonusDmg(i));
            if (minionObj == null) minionObj = MinionManager.GetMinions(Player.Position, Orbwalk.GetAutoAttackRange() + 50, MinionTypes.All, MinionTeam.NotAlly).Where(i => i.Health <= GetBonusDmg(i)).OrderBy(i => i.Distance(Player)).FirstOrDefault();
            if (minionObj == null) return;
            if (SkillW.IsReady() || Player.HasBuff("JaxEmpowerTwo", true))
            {
                Orbwalk.SetAttack(false);
                if (!Player.HasBuff("JaxEmpowerTwo", true)) SkillW.Cast(PacketCast());
                if (Player.HasBuff("JaxEmpowerTwo", true)) Player.IssueOrder(GameObjectOrder.AttackUnit, minionObj);
                Orbwalk.SetAttack(true);
            }
            else if (RCount >= 2)
            {
                Orbwalk.SetAttack(false);
                Player.IssueOrder(GameObjectOrder.AttackUnit, minionObj);
                Orbwalk.SetAttack(true);
            }
        }

        private void WardJump(Vector3 Pos)
        {
            if (!SkillQ.IsReady()) return;
            bool IsWard = false;
            var posJump = (Player.Distance(Pos) > SkillQ.Range) ? Player.Position + Vector3.Normalize(Pos - Player.Position) * 600 : Pos;
            foreach (var jumpObj in ObjectManager.Get<Obj_AI_Base>().Where(i => !i.IsMe && !(i is Obj_AI_Turret) && i.Distance(Player) <= SkillQ.Range + i.BoundingRadius && i.Distance(posJump) <= 230).OrderBy(i => i.Distance(posJump)))
            {
                SkillQ.CastOnUnit(jumpObj, PacketCast());
                if (jumpObj.Name.EndsWith("Ward") && jumpObj.IsMinion && jumpObj.IsAlly)
                {
                    IsWard = true;
                }
                else return;
            }
            if (!IsWard && GetWardSlot() != null && !WardCasted)
            {
                GetWardSlot().UseItem(posJump);
                WardCasted = true;
                Utility.DelayAction.Add(1000, () => WardCasted = false);
            }
        }

        private void KillSteal()
        {
            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillQ.Range) && i.Health < SkillQ.GetDamage(i) + GetBonusDmg(i) && i != targetObj);
            if (target != null && Player.Mana >= SkillQ.Instance.ManaCost)
            {
                if (SkillW.IsReady()) SkillW.Cast(PacketCast());
                if (SkillQ.IsReady() && Player.HasBuff("JaxEmpowerTwo", true)) SkillQ.CastOnUnit(target, PacketCast());
            }
        }

        private void UseItem(Obj_AI_Hero target)
        {
            if (Items.CanUseItem(Bilge) && Player.Distance(target) <= 450) Items.UseItem(Bilge, target);
            if (Items.CanUseItem(Blade) && Player.Distance(target) <= 450) Items.UseItem(Blade, target);
            if (Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
        }

        private double GetBonusDmg(Obj_AI_Base target)
        {
            double DmgItem = 0;
            if (Items.HasItem(Sheen) && (Items.CanUseItem(Sheen) || Player.HasBuff("sheen", true)) && Player.BaseAttackDamage > DmgItem) DmgItem = Player.BaseAttackDamage;
            if (Items.HasItem(Trinity) && (Items.CanUseItem(Trinity) || Player.HasBuff("sheen", true)) && Player.BaseAttackDamage * 2 > DmgItem) DmgItem = Player.BaseAttackDamage * 2;
            double DmgSkill = 0;
            if (SkillW.IsReady() || Player.HasBuff("JaxEmpowerTwo", true)) DmgSkill += SkillW.GetDamage(target);
            if (RCount >= 2) DmgSkill += SkillR.GetDamage(target);
            return DmgSkill + Player.CalcDamage(target, Damage.DamageType.Physical, Player.BaseAttackDamage + Player.FlatPhysicalDamageMod + DmgItem);
        }
    }
}