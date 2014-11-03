using System;
using System.Linq;
using System.Collections.Generic;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using LX_Orbwalker;

namespace Master
{
    class Program
    {
        public static Obj_AI_Hero Player = ObjectManager.Player, targetObj = null;
        private static TargetSelector selectTarget;
        public static Spell SkillQ, SkillW, SkillE, SkillR;
        private static SpellDataInst FData, SData, IData;
        public static Int32 Tiamat = 3077, Hydra = 3074, Blade = 3153, Bilge = 3144, Rand = 3143, Youmuu = 3142;
        public static Menu Config;
        public static String Name;
        public static Boolean PacketCast = false;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        private static void OnGameLoad(EventArgs args)
        {
            Game.PrintChat("<font color = \"#00bfff\">Master Series</font> by <font color = \"#9370db\">Brian</font>");
            Game.PrintChat("<font color = \"#ffa500\">Feel free to donate via Paypal to:</font> <font color = \"#ff4500\">dcbrian01@gmail.com</font>");
            Name = Player.ChampionName;
            Config = new Menu("Master Of " + Name, "Master_" + Name, true);

            Config.AddSubMenu(new Menu("Target Selector", "TSSettings"));
            Config.SubMenu("TSSettings").AddItem(new MenuItem("tsMode", "Mode").SetValue(new StringList(new[] { "Auto", "Most AD", "Most AP", "Less Attack", "Less Cast", "Low Hp", "Closest", "Near Mouse" })));
            Config.SubMenu("TSSettings").AddItem(new MenuItem("tsFocus", "Forced Target").SetValue(true));
            Config.SubMenu("TSSettings").AddItem(new MenuItem("tsDraw", "Draw Target").SetValue(true));
            selectTarget = new TargetSelector(2000, TargetSelector.TargetingMode.AutoPriority);

            var OWMenu = new Menu("Orbwalker", "Orbwalker");
            LXOrbwalker.AddToMenu(OWMenu);
            Config.AddSubMenu(OWMenu);

            try
            {
                if (Activator.CreateInstance(null, "Master." + Name) != null)
                {
                    var QData = Player.Spellbook.GetSpell(SpellSlot.Q);
                    var WData = Player.Spellbook.GetSpell(SpellSlot.W);
                    var EData = Player.Spellbook.GetSpell(SpellSlot.E);
                    var RData = Player.Spellbook.GetSpell(SpellSlot.R);
                    //Game.PrintChat("{0}/{1}/{2}/{3}", QData.SData.CastRange[0], WData.SData.CastRange[0], EData.SData.CastRange[0], RData.SData.CastRange[0]);
                    FData = Player.SummonerSpellbook.GetSpell(Player.GetSpellSlot("summonerflash"));
                    SData = Player.SummonerSpellbook.GetSpell(Player.GetSpellSlot("summonersmite"));
                    IData = Player.SummonerSpellbook.GetSpell(Player.GetSpellSlot("summonerdot"));
                    Game.OnGameUpdate += OnGameUpdate;
                    Drawing.OnDraw += OnDraw;
                    SkinChanger(null, null);
                }
            }
            catch
            {
                Game.PrintChat("[Master Series] => {0} Not Support !", Name);
            }
            Config.AddToMainMenu();
        }

        private static void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead) return;
            targetObj = GetTarget();
            var newTarget = Hud.SelectedUnit;
            if (newTarget != null && newTarget.IsValid && newTarget is Obj_AI_Hero && (newTarget as Obj_AI_Hero).IsValidTarget(2000)) targetObj = (Obj_AI_Hero)newTarget;
            LXOrbwalker.ForcedTarget = Config.Item("tsFocus").GetValue<bool>() ? targetObj : null;
        }

        private static void OnDraw(EventArgs args)
        {
            if (Player.IsDead || !Config.Item("tsDraw").GetValue<bool>() || targetObj == null) return;
            Utility.DrawCircle(targetObj.Position, 130, Color.Red);
        }

        private static Obj_AI_Hero GetTarget()
        {
            switch (Config.Item("tsMode").GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.AutoPriority);
                    break;
                case 1:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.MostAD);
                    break;
                case 2:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.MostAP);
                    break;
                case 3:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.LessAttack);
                    break;
                case 4:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.LessCast);
                    break;
                case 5:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.LowHP);
                    break;
                case 6:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.Closest);
                    break;
                case 7:
                    selectTarget.SetTargetingMode(TargetSelector.TargetingMode.NearMouse);
                    break;
            }
            return selectTarget.Target;
        }

        public static void Orbwalk(Obj_AI_Base target)
        {
            LXOrbwalker.Orbwalk(Game.CursorPos, (target != null && LXOrbwalker.InAutoAttackRange(target)) ? target : null);
        }

        public static bool CanKill(Obj_AI_Base target, Spell Skill, int Stage = 0)
        {
            return (Skill.GetHealthPrediction(target) + 20 < Skill.GetDamage(target, Stage)) ? true : false;
        }

        public static void SkinChanger(object sender, OnValueChangeEventArgs e)
        {
            Utility.DelayAction.Add(35, () => Packet.S2C.UpdateModel.Encoded(new Packet.S2C.UpdateModel.Struct(Player.NetworkId, Config.Item(Name + "SkinID").GetValue<Slider>().Value, Name)).Process());
        }

        public static List<Obj_AI_Base> CheckingCollision(Obj_AI_Base from, Obj_AI_Base target, Spell Skill)
        {
            var ListCol = new List<Obj_AI_Base>();
            foreach (var Col in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget(Skill.Range) && !(i is Obj_AI_Turret) && Skill.GetPrediction(i).Hitchance >= HitChance.Medium && i != target))
            {
                var Segment = Col.Position.To2D().ProjectOn(from.Position.To2D(), (from.Position + Vector3.Normalize(target.Position - from.Position) * Skill.Range).To2D());
                if (Segment.IsOnSegment && Col.Position.Distance(new Vector3(Segment.SegmentPoint.X, Col.Position.Y, Segment.SegmentPoint.Y)) <= Col.BoundingRadius + Skill.Width) ListCol.Add(Col);
            }
            return ListCol.Distinct().ToList();
        }

        public static bool SmiteCollision(Obj_AI_Hero target, Spell Skill)
        {
            var Col1 = CheckingCollision(Player, target, Skill);
            if (Col1.Count == 0 || Col1.Count > 1) return false;
            if (Skill.InRange(target.Position) && Col1.Count == 1 && (Col1.First() is Obj_AI_Minion))
            {
                if (CastSmite(Col1.First()))
                {
                    Skill.Cast(Skill.GetPrediction(target).CastPosition, PacketCast);
                    return true;
                }
            }
            return false;
        }

        public static bool FlashReady()
        {
            return (FData != null && FData.Slot != SpellSlot.Unknown && FData.State == SpellState.Ready);
        }

        public static bool SmiteReady()
        {
            return (SData != null && SData.Slot != SpellSlot.Unknown && SData.State == SpellState.Ready);
        }

        public static bool IgniteReady()
        {
            return (IData != null && IData.Slot != SpellSlot.Unknown && IData.State == SpellState.Ready);
        }

        public static bool CastFlash(Vector3 pos)
        {
            return (FlashReady() && Player.SummonerSpellbook.CastSpell(FData.Slot, pos));
        }

        public static bool CastSmite(Obj_AI_Base target)
        {
            if (SmiteReady() && target.IsValidTarget(SData.SData.CastRange[0]) && target.Health <= Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Smite))
            {
                Player.SummonerSpellbook.CastSpell(SData.Slot, target);
                return true;
            }
            return false;
        }

        public static bool CastIgnite(Obj_AI_Hero target)
        {
            if (IgniteReady() && target.IsValidTarget(IData.SData.CastRange[0]) && HealthPrediction.GetHealthPrediction(target, (int)(Player.Distance(target) / 1500 * 1000 + 250)) + 20 < Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite))
            {
                Player.SummonerSpellbook.CastSpell(IData.Slot, target);
                return true;
            }
            return false;
        }

        public static InventorySlot GetWardSlot()
        {
            Int32[] wardIds = { 3340, 3361, 3205, 3207, 3154, 3160, 2049, 2045, 2050, 2044 };
            InventorySlot warditem = null;
            foreach (var wardId in wardIds)
            {
                warditem = Player.InventoryItems.FirstOrDefault(i => i.Id == (ItemId)wardId);
                if (warditem != null && Player.Spellbook.Spells.First(i => (Int32)i.Slot == warditem.Slot + 4).State == SpellState.Ready) return warditem;
            }
            return warditem;
        }
    }
}