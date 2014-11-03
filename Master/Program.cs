using System;
using System.Linq;
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
        private static Obj_AI_Hero focusObj = null;
        private static TargetSelector selectTarget;
        public static Spell SkillQ, SkillW, SkillE, SkillR;
        private static SpellDataInst FData, SData, IData;
        public static Int32 Tiamat = 3077, Hydra = 3074, Blade = 3153, Bilge = 3144, Rand = 3143, Youmuu = 3142;
        public static Menu Config;
        public static String Name;
        public static Boolean PacketCast = false;
        public static InventorySlot Ward = null;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        private static void OnGameLoad(EventArgs args)
        {
            Name = Player.ChampionName;
            Config = new Menu("Master Of " + Name, "Master_" + Name, true);

            Config.AddSubMenu(new Menu("Target Selector", "TSSettings"));
            Config.SubMenu("TSSettings").AddItem(new MenuItem("tsMode", "Mode").SetValue(new StringList(new[] { "Auto", "Most AD", "Most AP", "Less Attack", "Less Cast", "Low Hp", "Closest", "Near Mouse" })));
            Config.SubMenu("TSSettings").AddItem(new MenuItem("tsFocus", "Forced Target").SetValue(true));
            Config.SubMenu("TSSettings").AddItem(new MenuItem("tsDraw", "Draw Target").SetValue(true));
            selectTarget = new TargetSelector(1500, TargetSelector.TargetingMode.AutoPriority);

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
                    Game.OnWndProc += OnWndProc;
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
            if (Config.Item("tsFocus").GetValue<bool>() && focusObj.IsValidTarget())
            {
                LXOrbwalker.ForcedTarget = focusObj;
                targetObj = focusObj;
            }
            else LXOrbwalker.ForcedTarget = null;
        }

        private static void OnDraw(EventArgs args)
        {
            if (Player.IsDead || !Config.Item("tsDraw").GetValue<bool>()) return;
            if (Config.Item("tsFocus").GetValue<bool>())
            {
                if (targetObj != null) Utility.DrawCircle((focusObj != null) ? focusObj.Position : targetObj.Position, 130, (focusObj != null) ? Color.Blue : Color.Red);
            }
            else if (targetObj != null) Utility.DrawCircle(targetObj.Position, 130, Color.Red);
        }

        private static void OnWndProc(WndEventArgs args)
        {
            if (MenuGUI.IsChatOpen || Player.IsDead || !Config.Item("tsFocus").GetValue<bool>()) return;
            if (args.Msg == (uint)WindowsMessages.WM_LBUTTONDOWN)
            {
                focusObj = null;
                foreach (var obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget() && i.Distance(Game.CursorPos) <= 130)) focusObj = obj;
            }
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

        public static void SkinChanger(object sender, OnValueChangeEventArgs e)
        {
            Utility.DelayAction.Add(35, () => Packet.S2C.UpdateModel.Encoded(new Packet.S2C.UpdateModel.Struct(Player.NetworkId, Config.Item(Name + "SkinID").GetValue<Slider>().Value, Name)).Process());
        }

        public static bool CheckingCollision(Obj_AI_Hero target, Spell Skill, bool Smite = true)
        {
            foreach (var col in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget(Skill.Range) && !(i is Obj_AI_Turret) && i != target))
            {
                var Segment = Geometry.ProjectOn(Skill.GetPrediction(col).CastPosition.To2D(), Player.Position.To2D(), (Player.Position + Vector3.Normalize(Skill.GetPrediction(target).CastPosition - Player.Position) * Skill.Range).To2D());
                if (Segment.IsOnSegment && Skill.GetPrediction(col).CastPosition.Distance(new Vector3(Segment.SegmentPoint.X, col.Position.Y, Segment.SegmentPoint.Y)) < col.BoundingRadius + Skill.Width - 30 && Skill.GetPrediction(col).Hitchance >= HitChance.High)
                {
                    if (Smite)
                    {
                        return (col is Obj_AI_Minion && CastSmite(col)) ? true : false;
                    }
                    else return true;
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
            if (IgniteReady() && target.IsValidTarget(IData.SData.CastRange[0]) && target.Health <= Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite))
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