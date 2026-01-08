using OpenTK.Windowing.Common.Input;
using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SFEngine.SFUnPak;
using SpellforceDataEditor.SFCFF;
using SpellforceDataEditor.SFCFF.category_forms;
using SpellforceDataEditor.SFCFF.helper_forms;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Windows.Forms;
using Windows.Security.ExchangeActiveSyncProvisioning;


namespace SpellforceDataEditor.special_forms
{
    public partial class SpelllforceCFFEditor : Form
    {
        public struct MobModifierStructure
        {
            public float StrengthMod;
            public float StaminaMod;
            public float AgilityMod;
            public float DexterityMod;
            public float CharismaMod;
            public float IntelligenceMod;
            public float WisdomMod;

            public float ResistancesMod;

            public float WalkMod;
            public float FightMod;
            public float CastMod;

            public string Suffix;
        }

        public struct ItemModifierStructure
        {
            public float ArmorMod;

            public float StrengthMod;
            public float StaminaMod;
            public float AgilityMod;
            public float DexterityMod;
            public float CharismaMod;
            public float IntelligenceMod;
            public float WisdomMod;

            public float ResistancesMod;

            public float WalkMod;
            public float FightMod;
            public float CastMod;

            public float HealthMod;
            public float ManaMod;

            public float WeaponSpeedMod;
            public float MinDamageMod;
            public float MaxDamageMod;
            public float MaxRangeMod;

            public float SellMod;
            public float BuyMod;

            public string Suffix;
        }

        struct TraceElement
        {
            public int cat;   // category id
            public int elem;   // element index
        }

        public bool data_loaded { get; private set; } = false;

        private int selected_category_id = SFEngine.Utility.NO_INDEX;
        private int selected_element_index = SFEngine.Utility.NO_INDEX;
        private int copied_element_index = SFEngine.Utility.NO_INDEX;

        private SFControl ElementDisplay;        //a control which displays all element parameters
        public Dictionary<int, SFControl> CachedElementDisplays = new Dictionary<int, SFControl>();   // element names and descriptions are read from here

        //these parameters control item loading behavior
        private int elementselect_refresh_size = 1000; // how many items per refresh are loaded
        private int elementselect_refresh_rate = 50;   // in miliseconds

        // tracer
        List<TraceElement> trace_list = new();

        // undo/redo
        public UndoRedoQueue urq = new();
        CFFOperatorHistory undoredo_form = null;

        // search
        CategorySearchForm search_form = null;

        // references
        ReferencesForm ref_form = null;

        //constructor
        public SpelllforceCFFEditor()
        {
            InitializeComponent();

            if ((MainForm.mapedittool != null) && (MainForm.mapedittool.ready))    // gamedata is already loaded by this point
            {
                mapeditor_set_gamedata();
                MessageBox.Show("Gamedata editor is now synchronized with map editor! Any changes saved will permanently alter gamedata in your Spellforce directory.");
            }

            urq.OnUndoStateChange = OnUndoStateChange;
            urq.OnRedoStateChange = OnRedoStateChange;

#if DEBUG
            clipboardTooldebugToolStripMenuItem.Visible = true;
#endif
        }

        //load game data
        private void loadGameDatacffToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((MainForm.mapedittool != null) && (MainForm.mapedittool.ready))
            {
                MessageBox.Show("Can not open gamedata while Map Editor is open.");
                return;
            }

            LoadGamedataForm LoadGD = new LoadGamedataForm();
            if (LoadGD.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            bool success = false;
            switch (LoadGD.Mode)
            {
                case LoadGamedataForm.GDMode.FULL:
                    success = load_data(LoadGD.MainGDFileName);
                    break;
                case LoadGamedataForm.GDMode.MERGE:
                    success = load_data_merge(LoadGD.MainGDFileName, LoadGD.OtherGDFileName);
                    break;
                case LoadGamedataForm.GDMode.DIFF:
                    success = load_data_diff(LoadGD.MainGDFileName, LoadGD.OtherGDFileName);
                    break;
                default:
                    break;
            }

            if (success)
            {
                CategorySelect.Enabled = true;
                foreach (var cat in SFCategoryManager.gamedata.GetCategories())
                {
                    CategorySelect.Items.Add(Tuple.Create(cat.GetCategoryID(), $"{cat.GetName()} ({cat.GetNumOfItems()} items)"));
                    CachedElementDisplays.Add(cat.GetCategoryID(), get_element_display_from_category(cat.GetCategoryID()));
                    cat.SetOnElementAddedCallback(CFF_OnElementAdded);
                    cat.SetOnElementModifiedCallback(CFF_OnElementModified);
                    cat.SetOnElementRemovedCallback(CFF_OnElementRemoved);
                    cat.SetOnSubElementAddedCallback(CFF_OnSubElementAdded);
                    cat.SetOnSubElementModifiedCallback(CFF_OnSubElementModified);
                    cat.SetOnSubElementRemovedCallback(CFF_OnSubElementRemoved);
                    cat.EnableUndoRedo(urq);

                }

                data_loaded = true;

                CategorySelect.SelectedIndex = 0;

                GC.Collect();
            }
        }

        public bool load_data(string fname)
        {
            if (data_loaded)
            {
                if (close_data() == DialogResult.Cancel)
                {
                    return false;
                }
            }

            labelStatus.Text = "Loading...";
            statusStrip1.Refresh();

            SFGameDataNew gamedata = new SFGameDataNew();

            if (gamedata.Load(fname) < 0)
            {
                labelStatus.Text = "Failed to open file " + fname;
                return false;
            }

            SFCategoryManager.Set(gamedata);

            Text = "GameData Editor - " + fname;
            labelStatus.Text = "Ready";

            return true;
        }

        public bool load_data_merge(string fname_orig, string fname_other)
        {
            if (data_loaded)
            {
                if (close_data() == DialogResult.Cancel)
                {
                    return false;
                }
            }

            labelStatus.Text = "Loading...";
            statusStrip1.Refresh();

            SFGameDataNew gamedata = new SFGameDataNew();
            if (gamedata.Load(fname_orig) < 0)
            {
                labelStatus.Text = "Failed to open file " + fname_orig;
                return false;
            }
            SFGameDataNew gamedata2 = new SFGameDataNew();
            if (gamedata2.Load(fname_other) < 0)
            {
                labelStatus.Text = "Failed to open file " + fname_other;
                return false;
            }
            SFGameDataNew gamedata3 = new SFGameDataNew();
            if (gamedata3.Merge(gamedata, gamedata2) != 0)
            {
                labelStatus.Text = "Failed to merge selected gamedata files";
                return false;
            }

            SFCategoryManager.Set(gamedata3);

            Text = "GameData Editor - " + fname_orig + " merged with " + fname_other;
            labelStatus.Text = "Ready";

            return true;
        }

        public bool load_data_diff(string fname_orig, string fname_other)
        {
            if (data_loaded)
            {
                if (close_data() == DialogResult.Cancel)
                {
                    return false;
                }
            }

            labelStatus.Text = "Loading...";
            statusStrip1.Refresh();

            SFGameDataNew gamedata = new SFGameDataNew();
            if (gamedata.Load(fname_orig) < 0)
            {
                labelStatus.Text = "Failed to open file " + fname_orig;
                return false;
            }
            SFGameDataNew gamedata2 = new SFGameDataNew();
            if (gamedata2.Load(fname_other) < 0)
            {
                labelStatus.Text = "Failed to open file " + fname_other;
                return false;
            }
            SFGameDataNew gamedata3 = new SFGameDataNew();
            if (gamedata3.Diff(gamedata, gamedata2) != 0)
            {
                labelStatus.Text = "Failed to diff selected gamedata files";
                return false;
            }

            SFCategoryManager.Set(gamedata3);

            Text = "GameData Editor - " + fname_orig + " diffed with " + fname_other;
            labelStatus.Text = "Ready";

            return true;
        }

        // gamedata is already loaded, just connect with the gamedata editor
        public void mapeditor_set_gamedata()
        {
            SFCategoryManager.manual_SetGamedata();

            CategorySelect.Enabled = true;
            foreach (var cat in SFCategoryManager.gamedata.GetCategories())
            {
                CategorySelect.Items.Add(Tuple.Create(cat.GetCategoryID(), $"{cat.GetName()} ({cat.GetNumOfItems()} items)"));
                CachedElementDisplays.Add(cat.GetCategoryID(), get_element_display_from_category(cat.GetCategoryID()));
                cat.SetOnElementAddedCallback(CFF_OnElementAdded);
                cat.SetOnElementModifiedCallback(CFF_OnElementModified);
                cat.SetOnElementRemovedCallback(CFF_OnElementRemoved);
                cat.SetOnSubElementAddedCallback(CFF_OnSubElementAdded);
                cat.SetOnSubElementModifiedCallback(CFF_OnSubElementModified);
                cat.SetOnSubElementRemovedCallback(CFF_OnSubElementRemoved);
                cat.EnableUndoRedo(urq);
            }

            data_loaded = true;

            CategorySelect.SelectedIndex = 0;

            GC.Collect();

            Text = "GameData Editor - synchronized with MapEditor";
            labelStatus.Text = "Ready";
        }

        //save game data
        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            save_data();
        }

        public bool save_data()
        {
            if (!data_loaded)
            {
                return false;
            }

            ActiveControl = null;

            if ((MainForm.mapedittool != null) && (MainForm.mapedittool.ready))    // dont ask when synchronized
            {
                return save_data_full(SFUnPak.game_directory_name + "\\data\\GameData.cff");
            }
            else
            {
                SaveGamedataForm sgd = new SaveGamedataForm();
                if (sgd.ShowDialog() != DialogResult.OK)
                {
                    return false;
                }

                return save_data_full(sgd.MainGDFileName);
            }
        }

        public bool save_data_full(string fname)
        {
            labelStatus.Text = "Saving...";

            SFCategoryManager.gamedata.Save(fname);

            labelStatus.Text = "Saved";

            return true;
        }

        private SFControl get_element_display_from_category(int cat)
        {
            switch (cat)
            {
                case 2002:
                    return new Control1();
                case 2054:
                    return new Control2();
                case 2056:
                    return new Control3();
                case 2005:
                    return new Control4();
                case 2006:
                    return new Control5();
                case 2067:
                    return new Control6();
                case 2003:
                    return new Control7();
                case 2004:
                    return new Control8();
                case 2013:
                    return new Control9();
                case 2015:
                    return new Control10();
                case 2017:
                    return new Control11();
                case 2014:
                    return new Control12();
                case 2012:
                    return new Control13();
                case 2018:
                    return new Control14();
                case 2016:
                    return new Control15();
                case 2022:
                    return new Control16();
                case 2023:
                    return new Control17();
                case 2024:
                    return new Control18();
                case 2025:
                    return new Control19();
                case 2026:
                    return new Control20();
                case 2028:
                    return new Control21();
                case 2040:
                    return new Control22();
                case 2001:
                    return new Control23();
                case 2029:
                    return new Control24();
                case 2030:
                    return new Control25();
                case 2031:
                    return new Control26();
                case 2039:
                    return new Control27();
                case 2062:
                    return new Control28();
                case 2041:
                    return new Control29();
                case 2042:
                    return new Control30();
                case 2047:
                    return new Control31();
                case 2044:
                    return new Control32();
                case 2048:
                    return new Control33();
                case 2050:
                    return new Control34();
                case 2057:
                    return new Control35();
                case 2065:
                    return new Control36();
                case 2051:
                    return new Control37();
                case 2052:
                    return new Control38();
                case 2053:
                    return new Control39();
                case 2055:
                    return new Control40();
                case 2058:
                    return new Control41();
                case 2059:
                    return new Control42();
                case 2061:
                    return new Control43();
                case 2063:
                    return new Control44();
                case 2064:
                    return new Control45();
                case 2032:
                    return new Control46();
                case 2049:
                    return new Control47();
                case 2036:
                    return new Control48();
                case 2072:
                    return new Control49();
                default:
                    return null;
            }
        }

        //spawns a new control to display element data
        private void set_category_panel(int cat)
        {
            if (ElementDisplay != null)
            {
                if (ElementDisplay.category.GetCategoryID() == cat)
                {
                    return;
                }
                else
                {
                    ElementDisplayPanel.Controls.Remove(ElementDisplay);
                }
            }

            ElementDisplay = CachedElementDisplays[cat];
            ElementDisplay.BringToFront();

            labelDescription.SendToBack();

            ElementDisplayPanel.Controls.Add(ElementDisplay);
        }

        private void set_displayed_element(int cat_id, int elem_index)
        {
            set_category_panel(cat_id);

            ElementDisplay.Visible = true;
            ElementDisplay.set_element(elem_index);
            ElementDisplay.show_element();

            labelDescription.Text = ElementDisplay.get_description_string(elem_index);
            label_tracedesc.Text = ElementDisplay.get_element_string(elem_index);

            if (MainForm.viewer != null)
            {
                ElementDisplay.category.GetID(elem_index, out int elem_id);
                MainForm.viewer.GenerateScene(selected_category_id, elem_id);
            }
        }

        //what happens when you choose category from a list
        private void CategorySelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearSearch();
            if (CategorySelect.SelectedIndex == -1)
            {
                return;
            }

            int cat_id = ((Tuple<short, string>)CategorySelect.SelectedItem).Item1;

            // force textboxes to validate, submitting data
            Focus();

            // set visibility
            panelElemManipulate.Visible = false;
            panelElemCopy.Visible = false;
            ElementSelect.Enabled = true;

            // set current category
            if (selected_category_id != cat_id)
            {
                // clear copied element
                ButtonElemAdd.BackColor = SystemColors.Control;
                ButtonElemInsert.BackColor = SystemColors.Control;
            }
            selected_category_id = ((Tuple<short, string>)CategorySelect.SelectedItem).Item1;
            clear_copied();

            // set display form for elements of this category
            set_category_panel(selected_category_id);
            ElementDisplay.Visible = false;

            // clear all elements and start loading new elements
            ICategory ctg = CachedElementDisplays[cat_id].category;
            ElementSelect_refresh(ctg);

            // search panel setup
            SearchColumnID.Items.Clear();
            SearchColumnID.SelectedIndex = -1;
            SearchColumnID.Text = "";
            foreach (string s in ElementDisplay.column_dict.Keys)
            {
                SearchColumnID.Items.Add(s);
            }

            panelSearch.Visible = true;
            ContinueSearchButton.Enabled = false;
        }

        //what happens when you choose element from a list
        private void ElementSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ElementSelect.SelectedIndex == -1)
            {
                ElementDisplay.Visible = false;
                labelDescription.Text = "";
                return;
            }

            trace_clear();
            selected_element_index = ElementSelect.SelectedIndex;
            set_displayed_element(selected_category_id, ElementSelect.SelectedIndex);
            if (ref_form != null)
            {
                ref_form.FindElementReferences(selected_category_id, ElementSelect.SelectedIndex);
            }
        }

        //start loading all elements from a category
        public void ElementSelect_refresh(ICategory ctg)
        {
            ElementSelect.Items.Clear();

            labelDescription.Text = "";
            labelStatus.Text = "Loading...";
            clear_copied();

            trace_clear();
            RestartTimer();
        }

        private void ElementSelect_DrawItem(object sender, DrawItemEventArgs e)
        {
            bool selected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);
            // the check below didnt need to be there in .net framework, curious
            if (selected)
            {
                if (!e.Bounds.IntersectsWith(((ListBoxNoFlicker)sender).ClientRectangle))
                {
                    return;
                }
            }

            int index = e.Index;
            if (index >= 0 && index < ElementSelect.Items.Count)
            {
                ICategory ctg = CachedElementDisplays[selected_category_id].category;
                string text = ElementSelect.Items[index].ToString();
                Graphics g = e.Graphics;

                //background:
                SolidBrush backgroundBrush;
                if (selected)
                {
                    backgroundBrush = WinFormsUtility.BrushBackgroundElemSelected;
                }
                else
                {
                    backgroundBrush = WinFormsUtility.BrushBackgroundDefault;
                }

                g.FillRectangle(backgroundBrush, e.Bounds);

                //text:
                SolidBrush foregroundBrush = (selected) ? WinFormsUtility.BrushTextElemSelected : WinFormsUtility.BrushTextDefault;
                g.DrawString(text, e.Font, foregroundBrush, ElementSelect.GetItemRectangle(index).Location);
            }
        }


        //this is where elements are added if category is being refreshed
        private void ElementSelect_RefreshTimer_Tick(object sender, EventArgs e)
        {
            ElementSelect.BeginUpdate();

            ICategory ctg = CachedElementDisplays[selected_category_id].category;

            int max_items = ctg.GetNumOfItems();
            int loaded_items = ElementSelect.Items.Count;
            int last = Math.Min(max_items, loaded_items + elementselect_refresh_size);

            SFControl element_display = CachedElementDisplays[selected_category_id];
            for (; loaded_items < last; loaded_items++)
            {
                ElementSelect.Items.Add(element_display.get_element_string(loaded_items));
            }

            if (max_items == 0)
            {
                ProgressBar_Main.Value = 0;
            }
            else
            {
                ProgressBar_Main.Value = (int)(((Single)last / (Single)max_items) * ProgressBar_Main.Maximum);
            }
            if (last != max_items)
            {
                ElementSelect_RefreshTimer.Interval = elementselect_refresh_rate;
                ElementSelect_RefreshTimer.Start();
            }
            else
            {
                ProgressBar_Main.Visible = false;
                labelStatus.Text = "Ready";

                ElementSelect_RefreshTimer.Enabled = false;

                if (ElementSelect.Items.Count == 0)
                {
                    ElementSelect.SelectedIndex = -1;
                }
                else
                {
                    ElementSelect.SelectedIndex = 0;
                }

                if (max_items == ctg.GetNumOfItems())
                {
                    panelElemManipulate.Visible = true;
                }
                panelElemCopy.Visible = true;
            }

            ElementSelect.EndUpdate();
        }

        //timer can be restarted if elements are to be gradually filled into the list again
        private void RestartTimer()
        {
            ElementSelect_RefreshTimer.Enabled = true;
            ElementSelect_RefreshTimer.Interval = elementselect_refresh_rate;
            ElementSelect_RefreshTimer.Start();

            panelElemManipulate.Visible = false;
            panelElemCopy.Visible = false;

            ProgressBar_Main.Visible = true;
            ProgressBar_Main.Value = 0;
        }

        //close gamedata.cff
        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((MainForm.mapedittool != null) && (MainForm.mapedittool.ready))
            {
                MessageBox.Show("Can not close gamedata while Map Editor is open.");
                return;
            }
            if (data_loaded)
            {
                close_data();
            }
        }

        //actually clear all data and close gamedata.cff
        public DialogResult close_data()
        {
            //ask first to close current gamedata.cff, if user clicks Cancel, function return immediately
            DialogResult result;
            if (!data_loaded)
            {
                return DialogResult.No;
            }
            else if ((MainForm.mapedittool != null) && (MainForm.mapedittool.ready))
            {
                result = DialogResult.Yes;
            }
            else
            {
                result = MessageBox.Show("Do you want to save gamedata before quitting? (Recommended when synchronized with Map Editor)", "Save before quit?", MessageBoxButtons.YesNoCancel);
            }

            if (result == DialogResult.Yes)
            {
                if (!save_data())
                {
                    return DialogResult.Cancel;
                }
            }
            else if (result == DialogResult.Cancel)
            {
                return result;
            }


            foreach (var elemd in CachedElementDisplays)
            {
                elemd.Value.category.ClearCallbacks();
                elemd.Value.Dispose();
            }
            CachedElementDisplays.Clear();
            ElementDisplay = null;

            //close everything
            if (ElementSelect_RefreshTimer.Enabled)
            {
                ElementSelect_RefreshTimer.Stop();
                ElementSelect_RefreshTimer.Enabled = false;
            }
            ElementSelect.Items.Clear();
            ElementSelect.Enabled = false;

            if (undoredo_form != null)
            {
                undoredo_form.Close();
            }
            if (search_form != null)
            {
                search_form.Close();
            }
            if (ref_form != null)
            {
                ref_form.Close();
            }

            panelElemManipulate.Visible = false;
            panelElemCopy.Visible = false;
            ButtonElemAdd.BackColor = SystemColors.Control;
            ButtonElemInsert.BackColor = SystemColors.Control;

            labelDescription.Text = "";
            label_tracedesc.Text = "";

            CategorySelect.Items.Clear();
            CategorySelect.Enabled = false;

            panelSearch.Visible = false;
            ContinueSearchButton.Enabled = false;

            selected_category_id = SFEngine.Utility.NO_INDEX;
            selected_element_index = SFEngine.Utility.NO_INDEX;
            copied_element_index = SFEngine.Utility.NO_INDEX;

            labelStatus.Text = "";
            ProgressBar_Main.Visible = false;
            ProgressBar_Main.Value = 0;
            statusStrip1.Refresh();

            SFCategoryManager.UnloadAll();

            urq.Clear();
            data_loaded = false;

            Text = "GameData Editor";

            GC.Collect();

            return result;
        }

        //exit application
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        // called before closing the form
        private void AskBeforeExit(object sender, FormClosingEventArgs e)
        {
            if (data_loaded)
            {
                if ((MainForm.mapedittool != null) && (MainForm.mapedittool.ready))
                {
                    return;
                }

                DialogResult result = close_data();
                if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        private void clear_copied()
        {
            copied_element_index = SFEngine.Utility.NO_INDEX;
            ButtonElemAdd.BackColor = System.Drawing.SystemColors.ControlLight;
            ButtonElemInsert.BackColor = System.Drawing.SystemColors.ControlLight;
        }

        // tracer
        public bool trace_id(int cat_id, int elem_id)
        {
            if (!data_loaded)
            {
                return false;
            }

            // find element index
            if (!CachedElementDisplays.ContainsKey(cat_id))
            {
                return false;
            }

            ICategory cat = CachedElementDisplays[cat_id].category;
            if (!cat.GetItemIndex(elem_id, out int elem_index))
            {
                return false;
            }

            trace_list.Add(new() { cat = cat_id, elem = elem_index });
            set_displayed_element(cat_id, elem_index);
            buttonTracerBack.Visible = true;

            return true;
        }

        public void trace_back()
        {
            if (!data_loaded)
            {
                return;
            }

            if (trace_list.Count == 0)
            {
                buttonTracerBack.Visible = false;
                return;
            }
            trace_list.RemoveAt(trace_list.Count - 1);
            if (trace_list.Count == 0)
            {
                set_displayed_element(selected_category_id, selected_element_index);
                buttonTracerBack.Visible = false;
                return;
            }
            set_displayed_element(trace_list[^1].cat, trace_list[^1].elem);
        }

        public void trace_clear()
        {
            if (!data_loaded)
            {
                return;
            }

            trace_list.Clear();
            buttonTracerBack.Visible = false;
        }

        private void buttonTracerBack_Click(object sender, EventArgs e)
        {
            trace_back();
        }

        // CFF callbacks
        public void CFF_OnElementAdded(int cat_id, int elem_index)
        {
            if (!data_loaded)
            {
                return;
            }

            // update category name in combobox
            for (int i = 0; i < CategorySelect.Items.Count; i++)
            {
                var item = (Tuple<short, string>)(CategorySelect.Items[i]);
                if (item.Item1 == cat_id)
                {

                    CategorySelect.SelectedIndexChanged -= CategorySelect_SelectedIndexChanged;
                    CategorySelect.Items[i] = new Tuple<short, string>((short)cat_id,
                        $"{CachedElementDisplays[cat_id].category.GetName()} ({CachedElementDisplays[cat_id].category.GetNumOfItems()} items)");
                    CategorySelect.SelectedIndexChanged += CategorySelect_SelectedIndexChanged;
                    break;
                }
            }

            // if selected category is the same, add the element to the list and select the element
            if (selected_category_id == cat_id)
            {
                search_form?.OnItemAdd(cat_id, elem_index);

                // find index
                int list_index = elem_index;

                ElementSelect.Items.Insert(list_index, CachedElementDisplays[cat_id].get_element_string(elem_index));
                ElementSelect.SelectedIndex = list_index;

                // update copied element reference
                if (copied_element_index != SFEngine.Utility.NO_INDEX)
                {
                    if (elem_index <= copied_element_index)
                    {
                        copied_element_index += 1;
                    }
                }
            }
        }


        public void CFF_OnElementModified(int cat_id, int elem_index)
        {
            if (!data_loaded)
            {
                return;
            }

            // if selected category is the same, change elem text
            if (selected_category_id == cat_id)
            {
                search_form?.OnItemModify(cat_id, elem_index);
                // find index
                int list_index = elem_index;

                ElementSelect.SelectedIndexChanged -= ElementSelect_SelectedIndexChanged;
                ElementSelect.Items[list_index] = CachedElementDisplays[cat_id].get_element_string(elem_index);
                ElementSelect.SelectedIndexChanged += ElementSelect_SelectedIndexChanged;
            }

            // if displayed element is the same, change description
            if (ElementDisplay == null)
            {
                return;
            }
            if ((ElementDisplay.category.GetCategoryID() == cat_id) && (ElementDisplay.current_element == elem_index))
            {
                ElementDisplay.set_element(elem_index);
                ElementDisplay.show_element();
                labelDescription.Text = ElementDisplay.get_description_string(elem_index);
                label_tracedesc.Text = ElementDisplay.get_element_string(elem_index);
            }
        }

        public void CFF_OnElementRemoved(int cat_id, int elem_index)
        {
            if (!data_loaded)
            {
                return;
            }

            // update category name in combobox
            for (int i = 0; i < CategorySelect.Items.Count; i++)
            {
                var item = (Tuple<short, string>)(CategorySelect.Items[i]);
                if (item.Item1 == cat_id)
                {

                    CategorySelect.SelectedIndexChanged -= CategorySelect_SelectedIndexChanged;
                    CategorySelect.Items[i] = new Tuple<short, string>((short)cat_id,
                        $"{CachedElementDisplays[cat_id].category.GetName()} ({CachedElementDisplays[cat_id].category.GetNumOfItems()} items)");
                    CategorySelect.SelectedIndexChanged += CategorySelect_SelectedIndexChanged;
                    break;
                }
            }

            // if selected category is the same, add the element to the list and select the element
            if (selected_category_id == cat_id)
            {
                search_form?.OnItemRemove(cat_id, elem_index);

                // find index
                int list_index = elem_index;
                int cur_list_index = ElementSelect.SelectedIndex;

                ElementSelect.Items.RemoveAt(list_index);

                // update copied element reference
                if (copied_element_index != SFEngine.Utility.NO_INDEX)
                {
                    if (elem_index == copied_element_index)
                    {
                        clear_copied();
                    }
                    else if (elem_index < copied_element_index)
                    {
                        copied_element_index -= 1;
                    }
                }

                // reselect the right element
                if (cur_list_index == list_index)
                {
                    if (cur_list_index == ElementSelect.Items.Count)
                    {
                        ElementSelect.SelectedIndex = cur_list_index - 1;
                    }
                    else
                    {
                        ElementSelect.SelectedIndex = cur_list_index;
                    }
                }
            }
            else
            {
                // if displayed element is the same, it must mean it was traced - move tracer back
                if (ElementDisplay == null)
                {
                    return;
                }
                if ((ElementDisplay.category.GetCategoryID() == cat_id) && (ElementDisplay.current_element == elem_index))
                {
                    trace_back();
                }
            }
        }

        public void CFF_OnSubElementAdded(int cat_id, int elem_index, int subelem_index)
        {
            if (!data_loaded)
            {
                return;
            }

            // if selected category is the same, change elem text
            if (selected_category_id == cat_id)
            {
                search_form?.OnItemModify(cat_id, elem_index);
                // find index
                int list_index = elem_index;
                ElementSelect.SelectedIndexChanged -= ElementSelect_SelectedIndexChanged;
                ElementSelect.Items[list_index] = CachedElementDisplays[cat_id].get_element_string(elem_index);
                ElementSelect.SelectedIndexChanged += ElementSelect_SelectedIndexChanged;
            }

            // if displayed element is the same, change description
            if (ElementDisplay == null)
            {
                return;
            }
            if ((ElementDisplay.category.GetCategoryID() == cat_id) && (ElementDisplay.current_element == elem_index))
            {
                ElementDisplay.on_add_subelement(subelem_index);
                labelDescription.Text = ElementDisplay.get_description_string(elem_index);
                label_tracedesc.Text = ElementDisplay.get_element_string(elem_index);
            }
        }

        public void CFF_OnSubElementModified(int cat_id, int elem_index, int subelem_index)
        {
            if (!data_loaded)
            {
                return;
            }

            // if selected category is the same, change elem text
            if (selected_category_id == cat_id)
            {
                search_form?.OnItemModify(cat_id, elem_index);
                // find index
                int list_index = elem_index;
                ElementSelect.SelectedIndexChanged -= ElementSelect_SelectedIndexChanged;
                ElementSelect.Items[list_index] = CachedElementDisplays[cat_id].get_element_string(elem_index);
                ElementSelect.SelectedIndexChanged += ElementSelect_SelectedIndexChanged;
            }

            // if displayed element is the same, change description
            if (ElementDisplay == null)
            {
                return;
            }
            if ((ElementDisplay.category.GetCategoryID() == cat_id) && (ElementDisplay.current_element == elem_index))
            {
                ElementDisplay.on_update_subelement(subelem_index);
                labelDescription.Text = ElementDisplay.get_description_string(elem_index);
                label_tracedesc.Text = ElementDisplay.get_element_string(elem_index);
            }
        }

        public void CFF_OnSubElementRemoved(int cat_id, int elem_index, int subelem_index)
        {
            if (!data_loaded)
            {
                return;
            }

            // if selected category is the same, change elem text
            if (selected_category_id == cat_id)
            {
                search_form?.OnItemModify(cat_id, elem_index);
                // find index
                int list_index = elem_index;
                ElementSelect.SelectedIndexChanged -= ElementSelect_SelectedIndexChanged;
                ElementSelect.Items[list_index] = CachedElementDisplays[cat_id].get_element_string(elem_index);
                ElementSelect.SelectedIndexChanged += ElementSelect_SelectedIndexChanged;
            }

            // if displayed element is the same, change description
            if (ElementDisplay == null)
            {
                return;
            }
            if ((ElementDisplay.category.GetCategoryID() == cat_id) && (ElementDisplay.current_element == elem_index))
            {
                ElementDisplay.on_remove_subelement(subelem_index);
                labelDescription.Text = ElementDisplay.get_description_string(elem_index);
                label_tracedesc.Text = ElementDisplay.get_element_string(elem_index);
            }
        }

        // add/insert/remove/copy/clear
        private void ButtonElemAdd_Click(object sender, EventArgs e)
        {
            // add empty
            if (!data_loaded)
            {
                return;
            }

            ICategory cat = CachedElementDisplays[selected_category_id].category;
            // get max id
            cat.GetLastUsedID(out int max_id, out int max_index);

            if (copied_element_index == SFEngine.Utility.NO_INDEX)
            {
                cat.AddID(max_index, max_id + 1);
            }
            else
            {
                cat.Copy(copied_element_index, max_index);
                cat.SetID(max_index, max_id + 1);
            }
        }

        private void ButtonElemInsert_Click(object sender, EventArgs e)
        {
            // add empty
            if (!data_loaded)
            {
                return;
            }

            int elem_index = ElementSelect.SelectedIndex;
            if (elem_index == SFEngine.Utility.NO_INDEX)
            {
                return;
            }

            // if cant insert new elem here, stop
            ICategory cat = CachedElementDisplays[selected_category_id].category;
            cat.GetID(elem_index, out int cur_id);
            if (!cat.CalculateNewItemIndex(cur_id + 1, out int new_index))
            {
                return;
            }

            if (copied_element_index == SFEngine.Utility.NO_INDEX)
            {
                cat.AddID(new_index, cur_id + 1);
            }
            else
            {
                cat.Copy(copied_element_index, new_index);
                cat.SetID(new_index, cur_id + 1);
            }
        }

        private void ButtonElemRemove_Click(object sender, EventArgs e)
        {
            if (!data_loaded)
            {
                return;
            }

            int elem_index = ElementSelect.SelectedIndex;
            if (elem_index == SFEngine.Utility.NO_INDEX)
            {
                return;
            }

            // remove selected item
            ICategory cat = CachedElementDisplays[selected_category_id].category;
            cat.Remove(elem_index);
        }

        private void ButtonElemCopy_Click(object sender, EventArgs e)
        {
            copied_element_index = ElementSelect.SelectedIndex;
            ButtonElemAdd.BackColor = System.Drawing.Color.DarkOrange;
            ButtonElemInsert.BackColor = System.Drawing.Color.DarkOrange;
        }

        private void ButtonElemClear_Click(object sender, EventArgs e)
        {
            clear_copied();
        }

        // undo/redo

        void Undo()
        {
            urq.Undo();
            undoredo_form?.OnUndo();
        }

        void Redo()
        {
            urq.Redo();
            undoredo_form?.OnRedo();
        }

        private void OnUndoStateChange(bool state)
        {
            undoCtrlZToolStripMenuItem.Enabled = state;
        }

        private void OnRedoStateChange(bool state)
        {
            redoCtrlYToolStripMenuItem.Enabled = state;
        }

        private void OnPush(IUndoRedo iur)
        {
            undoredo_form?.OnPush(iur);
        }

        private void OnPop()
        {
            undoredo_form?.OnPop();
        }

        private void undoCtrlZToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Undo();
        }

        private void redoCtrlYToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Redo();
        }

        private void operationHistoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!data_loaded)
            {
                return;
            }

            if (undoredo_form != null)
            {
                undoredo_form.Focus();
                return;
            }

            undoredo_form = new();
            undoredo_form.FormClosed += undoredo_form_FormClosed;
            undoredo_form.Show();

            urq.OnPush = OnPush;
            urq.OnPop = OnPop;
        }

        private void undoredo_form_FormClosed(object sender, EventArgs e)
        {
            if (undoredo_form == null)
            {
                return;
            }

            undoredo_form.FormClosed -= undoredo_form_FormClosed;
            urq.OnPush = null;
            urq.OnPop = null;

            undoredo_form = null;
            ContinueSearchButton.Enabled = false;
        }

        // search

        void OpenSearchForm()
        {
            if (!data_loaded)
            {
                return;
            }
            if (search_form != null)
            {
                search_form.Focus();
                return;
            }

            search_form = new CategorySearchForm();
            search_form.Show();
            search_form.FormClosed += search_form_FormClosed;
        }

        void search_form_FormClosed(object sender, EventArgs e)
        {
            if (search_form == null)
            {
                return;
            }

            search_form.FormClosed -= search_form_FormClosed;
            search_form = null;
        }

        private void checkSearchByColumn_CheckedChanged(object sender, EventArgs e)
        {
            SearchColumnID.Enabled = checkSearchByColumn.Enabled;
        }

        private List<int> DoSearch()
        {
            ICategory cat = CachedElementDisplays[selected_category_id].category;
            SFEngine.SFCFF.SearchOption so = SFEngine.SFCFF.SearchOption.NONE;
            List<int> result;
            string field_name = "";
            if (checkSearchByColumn.Checked)
            {
                if (SearchColumnID.SelectedIndex != SFEngine.Utility.NO_INDEX)
                {
                    field_name = CachedElementDisplays[selected_category_id].column_dict[SearchColumnID.Items[SearchColumnID.SelectedIndex].ToString()];
                }
            }

            if (radioSearchText.Checked)
            {
                so |= SFEngine.SFCFF.SearchOption.IS_STRING | SFEngine.SFCFF.SearchOption.IGNORE_CASE;
                string value = SearchQuery.Text;
                result = cat.QueryItems(value, field_name, so);

                // also search names
                if (field_name == "")
                {
                    List<int> element_string_result = new();
                    for (int i = 0; i < ElementSelect.Items.Count; i++)
                    {
                        string s = ElementSelect.Items[i].ToString();
                        if (s.Contains(value, StringComparison.InvariantCultureIgnoreCase))
                        {
                            element_string_result.Add(i);
                        }
                    }
                    result = result.Union(element_string_result).ToList();
                    result.Sort();
                }
            }
            else
            {
                so |= SFEngine.SFCFF.SearchOption.IS_NUMBER;
                if (radioSearchFlag.Checked)
                {
                    so |= SFEngine.SFCFF.SearchOption.NUMBER_AS_BITMASK;
                }
                int value = SFEngine.Utility.TryParseInt32(SearchQuery.Text);
                result = cat.QueryItems(value, field_name, so);
            }
            return result;
        }

        private void ClearSearch()
        {
            if (search_form != null)
            {
                search_form.Clear();
            }

            ContinueSearchButton.Enabled = false;
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            OpenSearchForm();

            List<int> result = DoSearch();

            ICategory cat = CachedElementDisplays[selected_category_id].category;
            search_form.Populate(cat.GetCategoryID(), result);

            ContinueSearchButton.Enabled = true;
        }

        private void ContinueSearchButton_Click(object sender, EventArgs e)
        {
            OpenSearchForm();

            List<int> result = DoSearch();

            ICategory cat = CachedElementDisplays[selected_category_id].category;
            search_form.Update(cat.GetCategoryID(), result);

            ContinueSearchButton.Enabled = true;
        }

        // references
        private void findAllReferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ref_form != null)
            {
                ref_form.Focus();
                ref_form.FindElementReferences(ElementDisplay.category.GetCategoryID(), ElementDisplay.current_element);
                return;
            }

            ref_form = new();
            ref_form.Show();
            ref_form.FormClosed += ref_form_FormClosed;

            if (ElementDisplay != null)
            {
                ref_form.FindElementReferences(ElementDisplay.category.GetCategoryID(), ElementDisplay.current_element);
            }
        }

        void ref_form_FormClosed(object sender, EventArgs e)
        {
            if (ref_form == null)
            {
                return;
            }

            ref_form.FormClosed -= ref_form_FormClosed;
            ref_form = null;
        }

        private void oblivionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var gd = SFCategoryManager.gamedata;
            if (gd == null)
            {
                MessageBox.Show("No GameData loaded.");
                return;
            }

            var MobModifierVeteran = new MobModifierStructure
            {
                StrengthMod = 1.5f,
                StaminaMod = 2.0f,
                AgilityMod = 1.0f,
                DexterityMod = 1.0f,
                CharismaMod = 1.0f,
                IntelligenceMod = 1.0f,
                WisdomMod = 1.0f,
                ResistancesMod = 1.0f,
                WalkMod = 1.0f,
                FightMod = 1.0f,
                CastMod = 1.0f,
                Suffix = "Veteran"
            };

            var MobModifierBossVeteran = new MobModifierStructure
            {
                StrengthMod = 2.0f,
                StaminaMod = 3.0f,
                AgilityMod = 1.1f,
                DexterityMod = 1.5f,
                CharismaMod = 1.5f,
                IntelligenceMod = 1.5f,
                WisdomMod = 1.5f,
                ResistancesMod = 1.05f,
                WalkMod = 1.1f,
                FightMod = 1.1f,
                CastMod = 1.2f,
                Suffix = "Veteran Boss"
            };

            var ItemModifierRare = new ItemModifierStructure
            {
                ArmorMod = 1.2f,

                StrengthMod = 1.15f,
                StaminaMod = 1.15f,
                AgilityMod = 1.15f,
                DexterityMod = 1.15f,
                CharismaMod = 1.15f,
                IntelligenceMod = 1.15f,
                WisdomMod = 1.15f,

                ResistancesMod = 1.05f,

                WalkMod = 1.1f,
                FightMod = 1.1f,
                CastMod = 1.1f,

                HealthMod = 1.15f,
                ManaMod = 1.15f,

                WeaponSpeedMod = 1.05f,
                MinDamageMod = 1.1f,
                MaxDamageMod = 1.1f,
                MaxRangeMod = 1.0f,

                SellMod = 1.25f,
                BuyMod = 2.0f,

                Suffix = "Rare"
            };

            //MobModifierElite = 
            //MobModifierChampion = 

            //DumpItemsNotSoldByMerchants(gd);
            //DumpUnusedEquippableItems(gd);
            //DumpQuestRewardEquippableItems(gd);
            //DumpQuestExclusiveEquippableItems(gd);
            //DumpChestExclusiveEquippableItems(gd);
            //DumpMobLootExclusiveEquippableItems(gd);
            //DumpMerchantExclusiveEquippableItems(gd);
            DumpSpellParameterSpecimens(gd);

            gd = CreateUnitVariant(gd, 777, MobModifierVeteran);
            gd = CreateItemVariant(gd, 684, ItemModifierRare);
            gd = ApplyBossModifiers(gd, MobModifierBossVeteran);
            // -------------------------------------------------
            // Notify editor
            // -------------------------------------------------
            SFCategoryManager.manual_SetGamedata();

            MessageBox.Show(
                $"Oblivion variant created.\n" +
                "Oblivion Mode."
            );
        }
        static string ReadContent(ref Category2016Item item)
        {
            unsafe
            {
                fixed (byte* ptr = item.Content)
                {
                    return Encoding.GetEncoding(1252)
                        .GetString(ptr, 256)   // buffer size (adjust if needed)
                        .TrimEnd('\0');
                }
            }
        }

        static void WriteContent(ref Category2016Item item, string text)
        {
            byte[] bytes = Encoding.GetEncoding(1252).GetBytes(text);

            unsafe
            {
                fixed (byte* ptr = item.Content)
                {
                    for (int i = 0; i < 256; i++)
                        ptr[i] = 0;

                    for (int i = 0; i < bytes.Length && i < 255; i++)
                        ptr[i] = bytes[i];
                }
            }
        }

        private SFGameDataNew CreateUnitVariant(SFGameDataNew gd, ushort baseUnitID, MobModifierStructure modifier)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            // -------------------------------------------------
            // Categories
            // -------------------------------------------------
            var unitCat = gd.c2024; // unit / creature data
            var statsCat = gd.c2005; // unit stats
            var locCat = gd.c2016; // localisation
            var equipCat = gd.c2025; // equipment

            // -------------------------------------------------
            // Find base unit
            // -------------------------------------------------
            Category2024Item baseUnit = default;
            bool found = false;

            foreach (var u in unitCat.Items)
            {
                if (u.UnitID == baseUnitID)
                {
                    baseUnit = u;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new Exception($"Base unit {baseUnitID} not found.");

            // -------------------------------------------------
            // Find base stats
            // -------------------------------------------------
            Category2005Item baseStats = default;
            found = false;

            foreach (var s in statsCat.Items)
            {
                if (s.StatsID == baseUnit.StatsID)
                {
                    baseStats = s;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new Exception($"Stats for unit {baseUnitID} not found.");

            // -------------------------------------------------
            // Allocate new IDs
            // -------------------------------------------------
            ushort newUnitID = 0;
            foreach (var u in unitCat.Items)
                if (u.UnitID > newUnitID)
                    newUnitID = u.UnitID;
            newUnitID++;

            ushort newStatsID = 0;
            foreach (var s in statsCat.Items)
                if (s.StatsID > newStatsID)
                    newStatsID = s.StatsID;
            newStatsID++;

            // -------------------------------------------------
            // Clone stats with modifiers
            // -------------------------------------------------
            var newStats = baseStats;
            newStats.StatsID = newStatsID;

            newStats.Strength = (ushort)(newStats.Strength * modifier.StrengthMod);
            newStats.Stamina = (ushort)(newStats.Stamina * modifier.StaminaMod);
            newStats.Agility = (ushort)(newStats.Agility * modifier.AgilityMod);
            newStats.Dexterity = (ushort)(newStats.Dexterity * modifier.DexterityMod);
            newStats.Charisma = (ushort)(newStats.Charisma * modifier.CharismaMod);
            newStats.Intelligence = (ushort)(newStats.Intelligence * modifier.IntelligenceMod);
            newStats.Wisdom = (ushort)(newStats.Wisdom * modifier.WisdomMod);

            // Resistances (grouped)
            newStats.ResistanceFire = (ushort)(newStats.ResistanceFire * modifier.ResistancesMod);
            newStats.ResistanceIce = (ushort)(newStats.ResistanceIce * modifier.ResistancesMod);
            newStats.ResistanceMind = (ushort)(newStats.ResistanceMind * modifier.ResistancesMod);
            newStats.ResistanceBlack = (ushort)(newStats.ResistanceBlack * modifier.ResistancesMod);

            // Speeds
            newStats.SpeedWalk = (ushort)(newStats.SpeedWalk * modifier.WalkMod);
            newStats.SpeedFight = (ushort)(newStats.SpeedFight * modifier.FightMod);
            newStats.SpeedCast = (ushort)(newStats.SpeedCast * modifier.CastMod);

            // -------------------------------------------------
            // Clone unit
            // -------------------------------------------------
            var newUnit = baseUnit;
            newUnit.UnitID = newUnitID;
            newUnit.StatsID = newStatsID;

            // -------------------------------------------------
            // Clone localisation
            // -------------------------------------------------
            ushort baseTextID = baseUnit.NameID;

            ushort newTextID = 0;
            foreach (var loc in locCat.Items)
                if (loc.TextID > newTextID)
                    newTextID = loc.TextID;
            newTextID++;

            int newLocBlockStart = locCat.Items.Count;
            locCat.Indices.Add(newLocBlockStart);

            bool anyLocCloned = false;

            foreach (var loc in locCat.Items.ToArray())
            {
                if (loc.TextID == baseTextID)
                {
                    var newLoc = loc;
                    newLoc.TextID = newTextID;

                    string text = ReadContent(ref newLoc);
                    WriteContent(ref newLoc, text + " [" + modifier.Suffix + "]");

                    locCat.Items.Add(newLoc);
                    anyLocCloned = true;
                }
            }

            if (!anyLocCloned)
                throw new Exception("No localisation entries found.");

            newUnit.NameID = newTextID;

            // -------------------------------------------------
            // Clone equipment
            // -------------------------------------------------
            int newEquipBlockStart = equipCat.Items.Count;
            equipCat.Indices.Add(newEquipBlockStart);

            foreach (var eq in equipCat.Items.ToArray())
            {
                if (eq.UnitID == baseUnitID)
                {
                    var newEq = eq;
                    newEq.UnitID = newUnitID;
                    equipCat.Items.Add(newEq);
                }
            }

            // -------------------------------------------------
            // Insert new unit & stats
            // -------------------------------------------------
            statsCat.Items.Add(newStats);
            unitCat.Items.Add(newUnit);

            return gd;
        }

        private SFGameDataNew CreateItemVariant(SFGameDataNew gd, ushort baseItemID, ItemModifierStructure modifier)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            var itemCat = gd.c2003;
            var locCat = gd.c2016;
            var uiCat = gd.c2012;
            var effCat = gd.c2014;
            var reqCat = gd.c2017;
            var armorCat = gd.c2004;
            var weapCat = gd.c2015;

            // -------------------------------------------------
            // Find base item
            // -------------------------------------------------
            Category2003Item baseItem = default;
            bool found = false;

            foreach (var it in itemCat.Items)
            {
                if (it.ItemID == baseItemID)
                {
                    baseItem = it;
                    found = true;
                    break;
                }
            }

            if (!found)
                throw new Exception($"Base item {baseItemID} not found.");

            // -------------------------------------------------
            // Allocate new ItemID
            // -------------------------------------------------
            ushort newItemID = 0;
            foreach (var it in itemCat.Items)
                if (it.ItemID > newItemID)
                    newItemID = it.ItemID;
            newItemID++;

            // -------------------------------------------------
            // Clone base item (c2003)
            // -------------------------------------------------
            var newItem = baseItem;
            newItem.ItemID = newItemID;

            newItem.BuyValue = (uint)(newItem.BuyValue * modifier.BuyMod);
            newItem.SellValue = (uint)(newItem.SellValue * modifier.SellMod);

            // -------------------------------------------------
            // Clone localisation (c2016)
            // -------------------------------------------------
            ushort baseTextID = baseItem.NameID;

            ushort newTextID = 0;
            foreach (var loc in locCat.Items)
                if (loc.TextID > newTextID)
                    newTextID = loc.TextID;
            newTextID++;

            int locBlockStart = locCat.Items.Count;
            locCat.Indices.Add(locBlockStart);

            bool anyLoc = false;
            foreach (var loc in locCat.Items.ToArray())
            {
                if (loc.TextID == baseTextID)
                {
                    var newLoc = loc;
                    newLoc.TextID = newTextID;

                    string text = ReadContent(ref newLoc);
                    WriteContent(ref newLoc, text + " [" + modifier.Suffix + "]");

                    locCat.Items.Add(newLoc);
                    anyLoc = true;
                }
            }

            if (!anyLoc)
                throw new Exception("Item localisation not found.");

            newItem.NameID = newTextID;

            // -------------------------------------------------
            // Clone UI data (c2012)
            // -------------------------------------------------
            int uiBlockStart = uiCat.Items.Count;
            uiCat.Indices.Add(uiBlockStart);

            foreach (var ui in uiCat.Items.ToArray())
            {
                if (ui.ItemID == baseItemID)
                {
                    var newUI = ui;
                    newUI.ItemID = newItemID;
                    uiCat.Items.Add(newUI);
                }
            }

            // -------------------------------------------------
            // Clone effects (c2014) – optional
            // -------------------------------------------------
            int effBlockStart = effCat.Items.Count;
            bool effFound = false;

            foreach (var ef in effCat.Items.ToArray())
            {
                if (ef.ItemID == baseItemID)
                {
                    if (!effFound)
                    {
                        effCat.Indices.Add(effBlockStart);
                        effFound = true;
                    }

                    var newEf = ef;
                    newEf.ItemID = newItemID;
                    effCat.Items.Add(newEf);
                }
            }

            // -------------------------------------------------
            // Clone requirements (c2017) – optional
            // -------------------------------------------------
            int reqBlockStart = reqCat.Items.Count;
            bool reqFound = false;

            foreach (var rq in reqCat.Items.ToArray())
            {
                if (rq.ItemID == baseItemID)
                {
                    if (!reqFound)
                    {
                        reqCat.Indices.Add(reqBlockStart);
                        reqFound = true;
                    }

                    var newRq = rq;
                    newRq.ItemID = newItemID;
                    reqCat.Items.Add(newRq);
                }
            }

            // -------------------------------------------------
            // Clone armor stats (c2004) – optional
            // -------------------------------------------------
            foreach (var ar in armorCat.Items.ToArray())
            {
                if (ar.ItemID == baseItemID)
                {
                    var newAr = ar;
                    newAr.ItemID = newItemID;

                    newAr.Armor = (short)(newAr.Armor * modifier.ArmorMod);
                    newAr.Strength = (short)(newAr.Strength * modifier.StrengthMod);
                    newAr.Stamina = (short)(newAr.Stamina * modifier.StaminaMod);
                    newAr.Agility = (short)(newAr.Agility * modifier.AgilityMod);
                    newAr.Dexterity = (short)(newAr.Dexterity * modifier.DexterityMod);
                    newAr.Charisma = (short)(newAr.Charisma * modifier.CharismaMod);
                    newAr.Intelligence = (short)(newAr.Intelligence * modifier.IntelligenceMod);
                    newAr.Wisdom = (short)(newAr.Wisdom * modifier.WisdomMod);

                    newAr.ResistFire = (short)(newAr.ResistFire * modifier.ResistancesMod);
                    newAr.ResistIce = (short)(newAr.ResistIce * modifier.ResistancesMod);
                    newAr.ResistMind = (short)(newAr.ResistMind * modifier.ResistancesMod);
                    newAr.ResistBlack = (short)(newAr.ResistBlack * modifier.ResistancesMod);

                    newAr.SpeedWalk = (short)(newAr.SpeedWalk * modifier.WalkMod);
                    newAr.SpeedFight = (short)(newAr.SpeedFight * modifier.FightMod);
                    newAr.SpeedCast = (short)(newAr.SpeedCast * modifier.CastMod);

                    newAr.Health = (short)(newAr.Health * modifier.HealthMod);
                    newAr.Mana = (short)(newAr.Mana * modifier.ManaMod);

                    armorCat.Items.Add(newAr);
                }
            }

            // -------------------------------------------------
            // Clone weapon stats (c2015) – optional
            // -------------------------------------------------
            foreach (var wp in weapCat.Items.ToArray())
            {
                if (wp.ItemID == baseItemID)
                {
                    var newWp = wp;
                    newWp.ItemID = newItemID;

                    newWp.MinDamage = (ushort)(newWp.MinDamage * modifier.MinDamageMod);
                    newWp.MaxDamage = (ushort)(newWp.MaxDamage * modifier.MaxDamageMod);
                    newWp.MaxRange = (ushort)(newWp.MaxRange * modifier.MaxRangeMod);
                    newWp.WeaponSpeed = (ushort)(newWp.WeaponSpeed * modifier.WeaponSpeedMod);

                    weapCat.Items.Add(newWp);
                }
            }

            // -------------------------------------------------
            // Insert base item last
            // -------------------------------------------------
            itemCat.Items.Add(newItem);

            return gd;
        }

        private bool TextContains(SFGameDataNew gd, ushort textID, string needle)
        {
            var locCat = gd.c2016;

            for (int i = 0; i < locCat.Items.Count; i++)
            {
                if (locCat.Items[i].TextID == textID)
                {
                    var loc = locCat.Items[i];
                    string text = ReadContent(ref loc);

                    if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        private HashSet<byte> CollectBossRaces(SFGameDataNew gd)
        {
            var bossRaces = new HashSet<byte>();
            var raceCat = gd.c2022;

            foreach (var race in raceCat.Items)
            {
                if (TextContains(gd, race.TextID, "Boss"))
                {
                    bossRaces.Add(race.RaceID);
                }
            }

            return bossRaces;
        }

        private SFGameDataNew ApplyBossModifiers(SFGameDataNew gd, MobModifierStructure modifier)
        {
            var unitCat = gd.c2024;
            var statsCat = gd.c2005;
            var locCat = gd.c2016;

            var bossRaces = CollectBossRaces(gd);

            for (int u = 0; u < unitCat.Items.Count; u++)
            {
                var unit = unitCat.Items[u];

                // -----------------------------
                // Resolve stats entry
                // -----------------------------
                int statsIndex = -1;
                for (int s = 0; s < statsCat.Items.Count; s++)
                {
                    if (statsCat.Items[s].StatsID == unit.StatsID)
                    {
                        statsIndex = s;
                        break;
                    }
                }

                if (statsIndex < 0)
                    continue;

                var stats = statsCat.Items[statsIndex];

                // -----------------------------
                // Check if this is a Boss race
                // -----------------------------
                if (!bossRaces.Contains(stats.UnitRace))
                    continue;

                // -----------------------------
                // Modify stats IN PLACE
                // -----------------------------
                stats.Strength = (ushort)(stats.Strength * modifier.StrengthMod);
                stats.Stamina = (ushort)(stats.Stamina * modifier.StaminaMod);
                stats.Agility = (ushort)(stats.Agility * modifier.AgilityMod);
                stats.Dexterity = (ushort)(stats.Dexterity * modifier.DexterityMod);
                stats.Charisma = (ushort)(stats.Charisma * modifier.CharismaMod);
                stats.Intelligence = (ushort)(stats.Intelligence * modifier.IntelligenceMod);
                stats.Wisdom = (ushort)(stats.Wisdom * modifier.WisdomMod);

                stats.ResistanceFire = (ushort)(stats.ResistanceFire * modifier.ResistancesMod);
                stats.ResistanceIce = (ushort)(stats.ResistanceIce * modifier.ResistancesMod);
                stats.ResistanceMind = (ushort)(stats.ResistanceMind * modifier.ResistancesMod);
                stats.ResistanceBlack = (ushort)(stats.ResistanceBlack * modifier.ResistancesMod);

                stats.SpeedWalk = (ushort)(stats.SpeedWalk * modifier.WalkMod);
                stats.SpeedFight = (ushort)(stats.SpeedFight * modifier.FightMod);
                stats.SpeedCast = (ushort)(stats.SpeedCast * modifier.CastMod);

                statsCat.Items[statsIndex] = stats;

                // -----------------------------
                // Modify localisation IN PLACE
                // -----------------------------
                for (int l = 0; l < locCat.Items.Count; l++)
                {
                    if (locCat.Items[l].TextID == unit.NameID)
                    {
                        var loc = locCat.Items[l];
                        string text = ReadContent(ref loc);

                        if (!text.Contains("[" + modifier.Suffix + "]"))
                        {
                            WriteContent(ref loc, text + " [" + modifier.Suffix + "]");
                            locCat.Items[l] = loc;
                        }
                    }
                }
            }

            return gd;
        }

        private void DumpItemsNotSoldByMerchants(SFGameDataNew gd)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            var merchantCat = gd.c2042;
            var itemCat = gd.c2003;
            var locCat = gd.c2016;

            // -------------------------------------------------
            // 1. Collect all ItemIDs sold by merchants
            // -------------------------------------------------
            var soldItemIDs = new HashSet<ushort>();

            foreach (var m in merchantCat.Items)
            {
                soldItemIDs.Add(m.ItemID);
            }

            // -------------------------------------------------
            // 2. Iterate over all items and find unsold ones
            // -------------------------------------------------
            var sb = new System.Text.StringBuilder();

            foreach (var item in itemCat.Items)
            {
                if (soldItemIDs.Contains(item.ItemID))
                    continue;

                // -------------------------------------------------
                // 3. Resolve English name via localisation
                // -------------------------------------------------
                string name = "<NO ENGLISH NAME>";

                for (int i = 0; i < locCat.Items.Count; i++)
                {
                    var loc = locCat.Items[i];

                    if (loc.TextID == item.NameID && loc.LanguageID == 1)
                    {
                        var locCopy = loc;
                        name = ReadContent(ref locCopy);
                        break;
                    }
                }

                sb.AppendLine($"{item.ItemID}\t{name}");
            }

            // -------------------------------------------------
            // 4. Write to text file
            // -------------------------------------------------
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Items_Not_Sold_By_Merchants.txt"
            );

            System.IO.File.WriteAllText(path, sb.ToString());

            MessageBox.Show(
                $"Item list written to:\n{path}",
                "Merchant analysis complete"
            );
        }
        private HashSet<ushort> CollectMobLootItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();
            var lootCat = gd.c2040;

            foreach (var l in lootCat.Items)
            {
                if (l.ItemID1 != 0) result.Add(l.ItemID1);
                if (l.ItemID2 != 0) result.Add(l.ItemID2);
                if (l.ItemID3 != 0) result.Add(l.ItemID3);
            }

            return result;
        }
        private HashSet<ushort> CollectChestLootItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();
            var chestCat = gd.c2065;

            foreach (var l in chestCat.Items)
            {
                if (l.ItemID1 != 0) result.Add(l.ItemID1);
                if (l.ItemID2 != 0) result.Add(l.ItemID2);
                if (l.ItemID3 != 0) result.Add(l.ItemID3);
            }

            return result;
        }
        private HashSet<ushort> CollectEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            // Armor data
            foreach (var a in gd.c2004.Items)
            {
                result.Add(a.ItemID);
            }

            // Weapon data
            foreach (var w in gd.c2015.Items)
            {
                result.Add(w.ItemID);
            }

            return result;
        }

        private void DumpUnusedEquippableItems(SFGameDataNew gd)
        {
            var mobLoot = CollectMobLootItemIDs(gd);
            var chestLoot = CollectChestLootItemIDs(gd);
            var equip = CollectEquippableItemIDs(gd);

            var itemCat = gd.c2003;
            var locCat = gd.c2016;

            var sb = new System.Text.StringBuilder();

            foreach (var item in itemCat.Items)
            {
                ushort id = item.ItemID;

                // Condition 1 & 2: not in mob loot AND not in chest loot
                if (mobLoot.Contains(id)) continue;
                if (chestLoot.Contains(id)) continue;

                // Condition 3: must be armor or weapon
                if (!equip.Contains(id)) continue;

                // Resolve name
                string name = "<NO NAME FOUND>";
                bool foundAny = false;

                for (int i = 0; i < locCat.Items.Count; i++)
                {
                    var loc = locCat.Items[i];

                    if (loc.TextID != item.NameID)
                        continue;

                    // First matching entry → fallback
                    if (!foundAny)
                    {
                        var locCopy = loc;
                        name = ReadContent(ref locCopy);
                        foundAny = true;
                    }

                    // Prefer English if present
                    if (loc.LanguageID == 1)
                    {
                        var locCopy = loc;
                        name = ReadContent(ref locCopy);
                        break;
                    }
                }

                sb.AppendLine($"{id}\t{name}");
            }

            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Unused_Equippable_Items.txt"
            );

            System.IO.File.WriteAllText(path, sb.ToString());

            MessageBox.Show(
                $"Unused equippable item list written to:\n{path}",
                "Item analysis complete"
            );
        }
        private HashSet<ushort> ExtractQuestRewardItemIDs(string luaText)
        {
            var result = new HashSet<ushort>();

            // Match numbers inside Items = { ... }
            var itemBlockRegex = new System.Text.RegularExpressions.Regex(
                @"Items\s*=\s*\{([^}]*)\}",
                System.Text.RegularExpressions.RegexOptions.Multiline
            );

            var numberRegex = new System.Text.RegularExpressions.Regex(@"\d+");

            foreach (System.Text.RegularExpressions.Match block in itemBlockRegex.Matches(luaText))
            {
                foreach (System.Text.RegularExpressions.Match num in numberRegex.Matches(block.Groups[1].Value))
                {
                    if (ushort.TryParse(num.Value, out ushort id))
                        result.Add(id);
                }
            }

            return result;
        }

        private bool IsEquippableItem(SFGameDataNew gd, ushort itemID)
        {
            foreach (var a in gd.c2004.Items) // armor
                if (a.ItemID == itemID)
                    return true;

            foreach (var w in gd.c2015.Items) // weapon
                if (w.ItemID == itemID)
                    return true;

            return false;
        }

        private string GetEnglishItemName(SFGameDataNew gd, ushort nameID)
        {
            foreach (var loc in gd.c2016.Items)
            {
                if (loc.TextID == nameID && loc.LanguageID == 1)
                {
                    var copy = loc;
                    return ReadContent(ref copy);
                }
            }
            return "<NO ENGLISH NAME>";
        }

        private void DumpQuestRewardEquippableItems(SFGameDataNew gd)
        {
            string path = Path.Combine(
                SFEngine.Settings.GameDirectory,
                "script",
                "gdsquestrewards.lua"
            );

            if (!File.Exists(path))
            {
                MessageBox.Show("gdsquestrewards.lua not found.");
                return;
            }

            string luaText = File.ReadAllText(path);
            var questItemIDs = ExtractQuestRewardItemIDs(luaText);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                if (!questItemIDs.Contains(item.ItemID))
                    continue;

                if (!IsEquippableItem(gd, item.ItemID))
                    continue;

                string name = GetEnglishItemName(gd, item.NameID);
                sb.AppendLine($"{item.ItemID}\t{name}");
            }

            string outPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Quest_Reward_Equippable_Items.txt"
            );

            File.WriteAllText(outPath, sb.ToString());

            MessageBox.Show($"Written:\n{outPath}", "Quest reward analysis complete");
        }

        private void DumpQuestExclusiveEquippableItems(SFGameDataNew gd)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            // -------------------------------------------------
            // Locate quest rewards file
            // -------------------------------------------------
            string questPath = Path.Combine(
                SFEngine.Settings.GameDirectory,
                "script",
                "gdsquestrewards.lua"
            );

            if (!File.Exists(questPath))
            {
                MessageBox.Show("gdsquestrewards.lua not found.");
                return;
            }

            // -------------------------------------------------
            // Parse quest reward item IDs
            // -------------------------------------------------
            string luaText = File.ReadAllText(questPath);
            HashSet<ushort> questItemIDs = ExtractQuestRewardItemIDs(luaText);

            // -------------------------------------------------
            // Collect merchant item IDs (c2042)
            // -------------------------------------------------
            HashSet<ushort> merchantItemIDs = new HashSet<ushort>();
            foreach (var m in gd.c2042.Items)
                merchantItemIDs.Add(m.ItemID);

            // -------------------------------------------------
            // Collect drop / chest item IDs
            // (reuse your existing logic here)
            // -------------------------------------------------
            HashSet<ushort> dropItemIDs = CollectDropEquippableItemIDs(gd);

            // -------------------------------------------------
            // Scan all items
            // -------------------------------------------------
            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort itemID = item.ItemID;

                // must be quest reward
                if (!questItemIDs.Contains(itemID))
                    continue;

                // must NOT be sold by merchants
                if (merchantItemIDs.Contains(itemID))
                    continue;

                // must NOT drop from enemies / chests
                if (dropItemIDs.Contains(itemID))
                    continue;

                // must be equippable
                if (!IsEquippableItem(gd, itemID))
                    continue;

                // resolve English name
                string name = GetEnglishItemName(gd, item.NameID);

                sb.AppendLine($"{itemID}\t{name}");
            }

            // -------------------------------------------------
            // Write output
            // -------------------------------------------------
            string outPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Quest_Exclusive_Equippable_Items.txt"
            );

            File.WriteAllText(outPath, sb.ToString());

            MessageBox.Show(
                $"Quest-exclusive equippable items written to:\n{outPath}",
                "Quest item analysis complete"
            );
        }


        private void FilterItemListByName(
    string inputPath,
    string outputPath,
    string[] forbiddenSubstrings)
        {
            var lines = File.ReadAllLines(inputPath);
            var sb = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                bool blocked = false;

                foreach (var s in forbiddenSubstrings)
                {
                    if (line.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                    sb.AppendLine(line);
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private HashSet<ushort> CollectDropEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            var gameDataType = gd.GetType();

            foreach (var field in gameDataType.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance))
            {
                object category = field.GetValue(gd);
                if (category == null)
                    continue;

                // Skip base item definitions
                if (category == gd.c2003)
                    continue;

                // Skip merchant stock
                if (category == gd.c2042)
                    continue;

                var itemsProp = category.GetType().GetProperty("Items");
                if (itemsProp == null)
                    continue;

                if (itemsProp.GetValue(category) is not System.Collections.IEnumerable items)
                    continue;

                foreach (var item in items)
                {
                    if (item == null)
                        continue;

                    var itemIDField = item.GetType().GetField("ItemID");
                    if (itemIDField == null)
                        continue;

                    object val = itemIDField.GetValue(item);
                    if (val is ushort itemID)
                    {
                        // Only care about equippables
                        if (IsEquippableItem(gd, itemID))
                            result.Add(itemID);
                    }
                }
            }

            return result;
        }

        private HashSet<ushort> CollectMobLootEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            foreach (var loot in gd.c2040.Items)
            {
                if (loot.ItemID1 != 0 && IsEquippableItem(gd, loot.ItemID1))
                    result.Add(loot.ItemID1);

                if (loot.ItemID2 != 0 && IsEquippableItem(gd, loot.ItemID2))
                    result.Add(loot.ItemID2);

                if (loot.ItemID3 != 0 && IsEquippableItem(gd, loot.ItemID3))
                    result.Add(loot.ItemID3);
            }

            return result;
        }

        private HashSet<ushort> CollectChestEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            foreach (var loot in gd.c2065.Items)
            {
                if (loot.ItemID1 != 0 && IsEquippableItem(gd, loot.ItemID1))
                    result.Add(loot.ItemID1);

                if (loot.ItemID2 != 0 && IsEquippableItem(gd, loot.ItemID2))
                    result.Add(loot.ItemID2);

                if (loot.ItemID3 != 0 && IsEquippableItem(gd, loot.ItemID3))
                    result.Add(loot.ItemID3);
            }

            return result;
        }

        private HashSet<ushort> CollectMerchantEquippableItemIDs(SFGameDataNew gd)
        {
            var result = new HashSet<ushort>();

            foreach (var m in gd.c2042.Items)
            {
                if (IsEquippableItem(gd, m.ItemID))
                    result.Add(m.ItemID);
            }

            return result;
        }

        private HashSet<ushort> CollectQuestEquippableItemIDs(SFGameDataNew gd)
        {
            string questPath = Path.Combine(
                SFEngine.Settings.GameDirectory,
                "script",
                "gdsquestrewards.lua"
            );

            var result = new HashSet<ushort>();

            if (!File.Exists(questPath))
                return result;

            string luaText = File.ReadAllText(questPath);
            var questIDs = ExtractQuestRewardItemIDs(luaText);

            foreach (var item in gd.c2003.Items)
            {
                if (questIDs.Contains(item.ItemID) &&
                    IsEquippableItem(gd, item.ItemID))
                {
                    result.Add(item.ItemID);
                }
            }

            return result;
        }

        private void DumpMerchantExclusiveEquippableItems(SFGameDataNew gd)
        {
            var merchantIDs = CollectMerchantEquippableItemIDs(gd);
            var mobIDs = CollectMobLootEquippableItemIDs(gd);
            var chestIDs = CollectChestEquippableItemIDs(gd);
            var questIDs = CollectQuestEquippableItemIDs(gd);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort id = item.ItemID;

                if (!merchantIDs.Contains(id)) continue;
                if (mobIDs.Contains(id)) continue;
                if (chestIDs.Contains(id)) continue;
                if (questIDs.Contains(id)) continue;

                sb.AppendLine($"{id}\t{GetEnglishItemName(gd, item.NameID)}");
            }

            WriteDump("Merchant_Exclusive_Equippable_Items.txt", sb.ToString());
        }

        private void DumpMobLootExclusiveEquippableItems(SFGameDataNew gd)
        {
            var mobIDs = CollectMobLootEquippableItemIDs(gd);
            var merchantIDs = CollectMerchantEquippableItemIDs(gd);
            var chestIDs = CollectChestEquippableItemIDs(gd);
            var questIDs = CollectQuestEquippableItemIDs(gd);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort id = item.ItemID;

                if (!mobIDs.Contains(id)) continue;
                if (merchantIDs.Contains(id)) continue;
                if (chestIDs.Contains(id)) continue;
                if (questIDs.Contains(id)) continue;

                sb.AppendLine($"{id}\t{GetEnglishItemName(gd, item.NameID)}");
            }

            WriteDump("Mob_Loot_Exclusive_Equippable_Items.txt", sb.ToString());
        }

        private void DumpChestExclusiveEquippableItems(SFGameDataNew gd)
        {
            var chestIDs = CollectChestEquippableItemIDs(gd);
            var mobIDs = CollectMobLootEquippableItemIDs(gd);
            var merchantIDs = CollectMerchantEquippableItemIDs(gd);
            var questIDs = CollectQuestEquippableItemIDs(gd);

            var sb = new System.Text.StringBuilder();

            foreach (var item in gd.c2003.Items)
            {
                ushort id = item.ItemID;

                if (!chestIDs.Contains(id)) continue;
                if (mobIDs.Contains(id)) continue;
                if (merchantIDs.Contains(id)) continue;
                if (questIDs.Contains(id)) continue;

                sb.AppendLine($"{id}\t{GetEnglishItemName(gd, item.NameID)}");
            }

            WriteDump("Chest_Exclusive_Equippable_Items.txt", sb.ToString());
        }
        private void WriteDump(string fileName, string content)
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                fileName
            );

            File.WriteAllText(path, content);

            MessageBox.Show(
                $"Written:\n{path}",
                "Item analysis complete"
            );
        }

        private unsafe List<int> ReadSpellParams(Category2002Item spell)
        {
            var result = new List<int>();

            const int PARAM_COUNT = 8;

            Category2002Item* s = &spell;
            {
                ushort* p = (ushort*)s->Params;

                for (int i = 0; i < PARAM_COUNT; i++)
                {
                    result.Add(p[i]);
                }
            }

            return result;
        }


        private string GetSpellLineName(SFGameDataNew gd, ushort spellLineID)
        {
            foreach (var sl in gd.c2054.Items)
            {
                if (sl.SpellLineID == spellLineID)
                {
                    return GetEnglishItemName(gd, sl.TextID);
                }
            }

            return $"<Unknown SpellLine {spellLineID}>";
        }

        private void DumpSpellParameterSpecimensOLD(SFGameDataNew gd)
        {
            var seenSpellLines = new HashSet<ushort>();
            var sb = new System.Text.StringBuilder();

            foreach (var spell in gd.c2002.Items)
            {
                ushort lineID = spell.SpellLineID;

                if (seenSpellLines.Contains(lineID))
                    continue;

                seenSpellLines.Add(lineID);

                string spellName = GetSpellLineName(gd, lineID);
                ushort spellID = spell.SpellID;

                sb.AppendLine($"{spellName} (SpellID: {spellID})");

                List<int> paramsList = ReadSpellParams(spell);

                for (int i = 0; i < paramsList.Count; i++)
                {
                    sb.AppendLine($"- [{i}] - {paramsList[i]}");
                }

                sb.AppendLine(); // spacing between spells
            }

            string outPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Spell_Param_Specimens.txt"
            );

            File.WriteAllText(outPath, sb.ToString());

            MessageBox.Show(
                $"Spell parameter specimens written to:\n{outPath}",
                "Spell analysis complete"
            );
        }

        private void DumpSpellParameterSpecimensOLD2(SFGameDataNew gd)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            var spellCat = gd.c2002;   // spells
            var spellLineCat = gd.c2054; // spell lines (types)
            var locCat = gd.c2016;     // localisation

            var sb = new StringBuilder();

            foreach (var spell in spellCat.Items)
            {
                ushort spellID = spell.SpellID;
                ushort spellTypeID = spell.SpellLineID;

                // -------------------------------------------------
                // Resolve spell name (same logic as editor list)
                // -------------------------------------------------
                string spellName = "<UNKNOWN SPELL>";

                if (spellLineCat.GetItemIndex(spellTypeID, out int spellLineIndex))
                {
                    ushort textID = spellLineCat.Items[spellLineIndex].TextID;

                    // prefer English (LanguageID == 1)
                    bool found = false;
                    foreach (var loc in locCat.Items)
                    {
                        if (loc.TextID == textID)
                        {
                            var locCopy = loc;
                            string text = ReadContent(ref locCopy);

                            if (!found)
                            {
                                spellName = text;
                                found = true;
                            }

                            if (loc.LanguageID == 1)
                            {
                                spellName = text;
                                break;
                            }
                        }
                    }
                }

                // -------------------------------------------------
                // Resolve parameter labels
                // -------------------------------------------------
                string[] labels = SFSpellDescriptor.get(spellTypeID);

                // -------------------------------------------------
                // Header
                // -------------------------------------------------
                sb.AppendLine(
                    $"{spellName} (SpellID: {spellID}, SpellTypeID: {spellTypeID})"
                );

                // -------------------------------------------------
                // Dump parameters
                // -------------------------------------------------
                const int PARAM_COUNT = 8; // storage size you already validated

                for (int i = 0; i < PARAM_COUNT; i++)
                {
                    uint value = spell.GetParam(i);
                    string label = (labels != null && i < labels.Length)
                        ? labels[i]
                        : "";

                    sb.AppendLine(
                        $"- [{i}] - {value}\t- {label}"
                    );
                }

                sb.AppendLine();
            }

            // -------------------------------------------------
            // Write output
            // -------------------------------------------------
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Spell_Parameter_Specimens.txt"
            );

            File.WriteAllText(path, sb.ToString());

            MessageBox.Show(
                $"Spell parameter dump written to:\n{path}",
                "Spell analysis complete"
            );
        }

        private void DumpSpellParameterSpecimens(SFGameDataNew gd)
        {
            if (gd == null)
                throw new ArgumentNullException(nameof(gd));

            var spellCat = gd.c2002;
            var spellLineCat = gd.c2054;
            var locCat = gd.c2016;

            var sb = new StringBuilder();

            // 👇 NEW: track processed spell types
            var dumpedSpellTypes = new HashSet<ushort>();

            foreach (var spell in spellCat.Items)
            {
                ushort spellID = spell.SpellID;
                ushort spellTypeID = spell.SpellLineID;

                // 👇 NEW: skip duplicates
                if (!dumpedSpellTypes.Add(spellTypeID))
                    continue;

                // -------------------------------------------------
                // Resolve spell name
                // -------------------------------------------------
                string spellName = "<UNKNOWN SPELL>";

                if (spellLineCat.GetItemIndex(spellTypeID, out int spellLineIndex))
                {
                    ushort textID = spellLineCat.Items[spellLineIndex].TextID;
                    bool found = false;

                    foreach (var loc in locCat.Items)
                    {
                        if (loc.TextID != textID)
                            continue;

                        var locCopy = loc;
                        string text = ReadContent(ref locCopy);

                        if (!found)
                        {
                            spellName = text;
                            found = true;
                        }

                        if (loc.LanguageID == 1) // English preferred
                        {
                            spellName = text;
                            break;
                        }
                    }
                }

                // -------------------------------------------------
                // Resolve parameter labels
                // -------------------------------------------------
                string[] labels = SFSpellDescriptor.get(spellTypeID);

                // -------------------------------------------------
                // Header
                // -------------------------------------------------
                sb.AppendLine(
                    $"{spellName} (SpellID: {spellID}, SpellTypeID: {spellTypeID})"
                );

                // -------------------------------------------------
                // Dump parameters
                // -------------------------------------------------
                const int PARAM_COUNT = 8;

                for (int i = 0; i < PARAM_COUNT; i++)
                {
                    uint value = spell.GetParam(i);
                    string label = (labels != null && i < labels.Length)
                        ? labels[i]
                        : "";

                    sb.AppendLine(
                        $"- [{i}] - {value}\t- {label}"
                    );
                }

                sb.AppendLine();
            }

            // -------------------------------------------------
            // Write output
            // -------------------------------------------------
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Spell_Parameter_Specimens.txt"
            );

            File.WriteAllText(path, sb.ToString());

            MessageBox.Show(
                $"Spell parameter specimens written to:\n{path}",
                "Spell analysis complete"
            );
        }




    }
}
