using Microsoft.Win32;
using OpenTK.Windowing.Common.Input;
using SFEngine.SFCFF;
using SFEngine.SFCFF.CTG;
using SFEngine.SFUnPak;
using SpellforceDataEditor.OblivionScripts;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Security.ExchangeActiveSyncProvisioning;
using static SpellforceDataEditor.OblivionScripts.SpellVarianting;


namespace SpellforceDataEditor.special_forms
{
    public partial class SpelllforceCFFEditor : Form
    {
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

        private async void oblivionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var gd = SFCategoryManager.gamedata;
            if (gd == null)
            {
                MessageBox.Show("No GameData loaded.");
                return;
            }

            var MobModifierVeteran = new UnitVarianting.MobModifierStructure
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

            var MobModifierElite = new UnitVarianting.MobModifierStructure
            {
                StrengthMod = 2.0f,
                StaminaMod = 3.0f,
                AgilityMod = 1.25f,
                DexterityMod = 1.25f,
                CharismaMod = 1.5f,
                IntelligenceMod = 1.5f,
                WisdomMod = 1.5f,
                ResistancesMod = 1.15f,
                WalkMod = 1.1f,
                FightMod = 1.2f,
                CastMod = 1.4f,
                Suffix = "Elite"
            };

            var MobModifierChampion = new UnitVarianting.MobModifierStructure
            {
                StrengthMod = 2.5f,
                StaminaMod = 4.0f,
                AgilityMod = 1.35f,
                DexterityMod = 1.45f,
                CharismaMod = 1.75f,
                IntelligenceMod = 1.75f,
                WisdomMod = 1.75f,
                ResistancesMod = 1.20f,
                WalkMod = 1.15f,
                FightMod = 1.3f,
                CastMod = 1.7f,
                Suffix = "Champion"
            };

            var MobModifierOblivion = new UnitVarianting.MobModifierStructure
            {
                StrengthMod = 3.25f,
                StaminaMod = 5.00f,
                AgilityMod = 1.50f,
                DexterityMod = 1.60f,
                CharismaMod = 2.00f,
                IntelligenceMod = 2.00f,
                WisdomMod = 2.00f,
                ResistancesMod = 1.25f,
                WalkMod = 1.25f,
                FightMod = 1.50f,
                CastMod = 2.50f,
                Suffix = "Oblivion"
            };

            var ItemModifierRare = new ItemVarianting.ItemModifierStructure
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

                WeaponSpeedMod = 1.00f,
                MinDamageMod = 1.1f,
                MaxDamageMod = 1.1f,
                MaxRangeMod = 1.0f,

                SellMod = 1.25f,
                BuyMod = 3.0f,

                Suffix = "Rare"
            };

            var ItemModifierMasterwork = new ItemVarianting.ItemModifierStructure
            {
                ArmorMod = 1.40f,

                StrengthMod = 1.30f,
                StaminaMod = 1.40f,
                AgilityMod = 1.25f,
                DexterityMod = 1.30f,
                CharismaMod = 1.20f,
                IntelligenceMod = 1.25f,
                WisdomMod = 1.25f,

                ResistancesMod = 1.10f,

                WalkMod = 1.15f,
                FightMod = 1.15f,
                CastMod = 1.15f,

                HealthMod = 1.25f,
                ManaMod = 1.25f,

                WeaponSpeedMod = 1.05f,
                MinDamageMod = 1.2f,
                MaxDamageMod = 1.3f,
                MaxRangeMod = 1.0f,

                SellMod = 2.00f,
                BuyMod = 7.0f,

                Suffix = "Masterwork"
            };

            var ItemModifierPerfect = new ItemVarianting.ItemModifierStructure
            {
                ArmorMod = 1.65f,

                StrengthMod = 1.40f,
                StaminaMod = 1.50f,
                AgilityMod = 1.30f,
                DexterityMod = 1.40f,
                CharismaMod = 1.30f,
                IntelligenceMod = 1.35f,
                WisdomMod = 1.35f,

                ResistancesMod = 1.15f,

                WalkMod = 1.20f,
                FightMod = 1.20f,
                CastMod = 1.20f,

                HealthMod = 1.40f,
                ManaMod = 1.40f,

                WeaponSpeedMod = 1.05f,
                MinDamageMod = 1.30f,
                MaxDamageMod = 1.50f,
                MaxRangeMod = 1.05f,

                SellMod = 3.00f,
                BuyMod = 10.0f,

                Suffix = "Perfect"
            };

            var ItemModifierLegendary = new ItemVarianting.ItemModifierStructure
            {
                ArmorMod = 1.85f,

                StrengthMod = 1.55f,
                StaminaMod = 1.65f,
                AgilityMod = 1.40f,
                DexterityMod = 1.50f,
                CharismaMod = 1.40f,
                IntelligenceMod = 1.50f,
                WisdomMod = 1.50f,

                ResistancesMod = 1.20f,

                WalkMod = 1.25f,
                FightMod = 1.30f,
                CastMod = 1.30f,

                HealthMod = 1.60f,
                ManaMod = 1.60f,

                WeaponSpeedMod = 1.10f,
                MinDamageMod = 1.40f,
                MaxDamageMod = 1.65f,
                MaxRangeMod = 1.10f,

                SellMod = 5.00f,
                BuyMod = 25.0f,

                Suffix = "Legendary"
            };

            var SpellModifierEmpowered = new SpellModifierStructure
            {
                Suffix = "Empowered",
                BuyPriceMult = 1.50f,
                SellPriceMult = 1.20f,

                Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 1.20f,
                        ManaCostMult = 1.10f,
                        RecastTimeMult = 1.00f,
                        CastTimeMult = 1.00f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.10f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.90f,
                        ManaCostMult = 1.10f,
                        RecastTimeMult = 1.00f,
                    },
                    Healing =
                    {
                        HealMult = 1.12f,
                        ManaCostMult = 1.15f,
                        CastTimeMult = 0.90f,
                        RecastTimeMult = 0.90f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.10f,
                        DurationMult = 1.10f,
                        ManaCostMult = 1.10f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.05f,
                        ManaCostMult = 1.20f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.90f,
                        RecastTimeMult = 1.00f,
                    }
                },

                // NEW: Summoning tier behavior
                Summoning =
                {
                    // Pacing
                    ManaCostMult = 1.10f,
                    CastTimeMult = 1.10f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.10f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = MobModifierVeteran,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
            };

            var SpellModifierSuperior = new SpellModifierStructure
            {
                Suffix = "Superior",
                BuyPriceMult = 6.00f,
                SellPriceMult = 2.00f,

                Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 1.50f,
                        ManaCostMult = 1.25f,
                        RecastTimeMult = 1.00f,
                        CastTimeMult = 0.90f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.25f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.80f,
                        ManaCostMult = 1.25f,
                        RecastTimeMult = 0.90f,
                    },
                    Healing =
                    {
                        HealMult = 1.25f,
                        ManaCostMult = 1.2f,
                        CastTimeMult = 0.80f,
                        RecastTimeMult = 0.80f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.20f,
                        DurationMult = 1.25f,
                        ManaCostMult = 1.20f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.10f,
                        ManaCostMult = 1.30f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.80f,
                        RecastTimeMult = 0.90f,
                    }
                },

                // NEW: Summoning tier behavior
                Summoning =
                {
                    // Pacing
                    ManaCostMult = 1.30f,
                    CastTimeMult = 1.20f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.20f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = MobModifierElite,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
            };

            var SpellModifierPerfected = new SpellModifierStructure
            {
                Suffix = "Perfected",
                BuyPriceMult = 10.00f,
                SellPriceMult = 3.00f,

                Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 1.80f,
                        ManaCostMult = 1.40f,
                        RecastTimeMult = 0.90f,
                        CastTimeMult = 0.80f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.40f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.75f,
                        ManaCostMult = 1.40f,
                        RecastTimeMult = 0.80f,
                    },
                    Healing =
                    {
                        HealMult = 1.40f,
                        ManaCostMult = 1.3f,
                        CastTimeMult = 0.75f,
                        RecastTimeMult = 0.70f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.30f,
                        DurationMult = 1.40f,
                        ManaCostMult = 1.30f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.20f,
                        ManaCostMult = 1.50f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.70f,
                        RecastTimeMult = 0.85f,
                    }
                },

                // NEW: Summoning tier behavior
                Summoning =
                {
                    // Pacing
                    ManaCostMult = 1.80f,
                    CastTimeMult = 1.40f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.40f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = MobModifierChampion,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
            };

            var SpellModifierArch = new SpellModifierStructure
            {
                Suffix = "Arch",
                BuyPriceMult = 20.00f,
                SellPriceMult = 4.00f,

                Direct =
                {
                    DirectDamage =
                    {
                        DamageMult = 2.00f,
                        ManaCostMult = 1.50f,
                        RecastTimeMult = 0.80f,
                        CastTimeMult = 0.70f,
                    },
                    DamageOverTime =
                    {
                        DamageMult = 1.50f,
                        DurationMult = 1.00f,
                        TickCountMult = 1.00f,
                        TickIntervalMult = 0.70f,
                        ManaCostMult = 1.50f,
                        RecastTimeMult = 0.75f,
                    },
                    Healing =
                    {
                        HealMult = 1.60f,
                        ManaCostMult = 1.4f,
                        CastTimeMult = 0.65f,
                        RecastTimeMult = 0.60f,
                    },
                    BuffDebuff =
                    {
                        PercentMult = 1.40f,
                        DurationMult = 1.50f,
                        ManaCostMult = 1.35f,
                        RecastTimeMult = 1.00f,
                    },
                    CrowdControl =
                    {
                        DurationMult = 1.35f,
                        ManaCostMult = 1.70f,
                        RecastTimeMult = 1.00f,
                    },
                    Utility =
                    {
                        ManaCostMult = 0.60f,
                        RecastTimeMult = 0.75f,
                    }
                },

                // NEW: Summoning tier behavior
                Summoning =
                {
                    // Pacing
                    ManaCostMult = 2.00f,
                    CastTimeMult = 1.70f,
                    RecastTimeMult = 1.00f,

                    // Upkeep / periodic costs (applies only if those params exist in descriptor)
                    TickIntervalMult = 1.00f,  // keep stable unless you intentionally want faster upkeep ticks
                    ManaPerTickMult  = 1.60f,  // make stronger summons slightly more expensive to sustain

                    // Summoned unit variant scaling
                    SummonedMobModifier = MobModifierOblivion,

                    // If SummonedMobModifier.Suffix is empty, it would inherit "Uncommon".
                    // Here it isn't empty ("Veteran"), but keeping this true is fine.
                    InheritSuffixToSummon = true
                }
            };

            //DumpItemsNotSoldByMerchants(gd);
            //DumpUnusedEquippableItems(gd);
            //DumpQuestRewardEquippableItems(gd);
            //DumpQuestExclusiveEquippableItems(gd);
            //DumpChestExclusiveEquippableItems(gd);
            //DumpMobLootExclusiveEquippableItems(gd);
            //DumpMerchantExclusiveEquippableItems(gd);

            var spellBlacklist = SpellVarianting.BuildSpellLineBlacklist();

            //OblivionScripts.HelperDumpers.DumpSpellParameterSpecimens(gd);
            //OblivionScripts.HelperDumpers.DumpSpellClassificationLookup(gd, SFEngine.Settings.LanguageID, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Spell_Classification_Lookup.txt"), SpellBlacklist);

            var blacklistPlayerRaces = VariantBlacklists.BuildUnitIDBlacklist_ByRaceRange(gd, 0, 6);
            var blacklistSummonables = VariantBlacklists.BuildUnitIDBlacklist_SummonableBySpellText(gd, "summon", followSubEffects: true);

            // Combined blacklist
            var unitVariantBlacklist = new HashSet<ushort>(blacklistPlayerRaces);
            unitVariantBlacklist.UnionWith(blacklistSummonables);


            var mobTierTable = new List<UnitVarianting.MobModifierStructure>
            {
                //MobModifierVeteran,
                MobModifierElite,
                //MobModifierChampion,
                MobModifierOblivion
            };

            //gd = UnitPromotion.PromoteUnitToHighestAndCreateBackCopies(
            //    gd,
            //    baseUnitID: 777,
            //    tierTable: mobTierTable,
            //    unitBlacklist: unitVariantBlacklist,
            //    out ushort promotedID,
            //    out ushort vetID,
            //    out ushort eliteID,
            //    out ushort champID,
            //    out ushort originalCopyID
            //);

            //gd = ItemVarianting.PromoteItemToHighestTierAndCreateBackCopies(
            //    gd,
            //    baseItemID: 3507,
            //    rare: ItemModifierRare,
            //    masterwork: ItemModifierMasterwork,
            //    perfect: ItemModifierPerfect,
            //    highest: ItemModifierLegendary,
            //    out var res
            //);

            var spellTierTable = new List<SpellModifierStructure>
            {
                //SpellModifierEmpowered,
                SpellModifierSuperior,
                //SpellModifierPerfected,
                SpellModifierArch
            };

            var itemTierTable = new List<ItemVarianting.ItemModifierStructure>
            {
                //ItemModifierRare,
                ItemModifierMasterwork,
                //ItemModifierPerfect,
                ItemModifierLegendary
            };

            var itemBlackList = new HashSet<ushort>();

            //gd = SpellPromotion.PromoteSpellWithScrollToHighestAndCreateBackCopies(
            //    gd, baseSpellID: 2153, spellTierTable, out var resSpell
            //);

            //gd = SpellVarianting.CreateSpellVariantAndGrantItems(gd, 175, SpellModifierUncommon);

            //gd = SpellVarianting.CreateSpellVariant(gd, 1, SpellModifierUncommon, out ushort newSpellID);
            //gd = ItemVarianting.CreateSpellScrollAndSpellbookForSpellVariant(gd, 1, newSpellID, SpellModifierUncommon, out ushort newScrollItemID, out ushort newSpellbookItemID);

            //gd = UnitVarianting.CreateUnitVariant(gd, 777, MobModifierVeteran);
            //gd = ItemVarianting.CreateItemVariant(gd, 684, ItemModifierRare);
            //gd = UnitVarianting.ApplyBossModifiers(gd, MobModifierBossVeteran);

            var registry = new VariantRegistry();

            using var cts = new CancellationTokenSource();

            // Your WinForms progress dialog (with ProgressBar + label + Cancel button)
            using var dlg = new ProgressForm();
            dlg.BindCancellation(cts);              // Cancel button calls cts.Cancel()
            dlg.Show(this);

            var progress = new Progress<ProgressInfo>(info =>
            {
                dlg.UpdateProgress(info);           // set label text + progress bar value
            });

            try
            {
                // Run the heavy work off the UI thread
                await Task.Run(() =>
                {
                    // 2) Promote units and register
                    //gd = VariantPipeline.BuildUnitVariantsAndRegister(
                    //    gd, mobTierTable, unitVariantBlacklist, registry,
                    //    progress: progress, cancellationToken: cts.Token
                    //);

                    // 3) Promote equippable items and register (already supports progress/cancel)
                    gd = VariantPipeline.BuildItemVariantsAndRegister(
                        gd, itemTierTable, itemBlackList, registry,
                        progress: progress, cancellationToken: cts.Token
                    );

                    //// 4) Promote scrollable spells and register (now supports progress/cancel + blacklist)
                    //gd = VariantPipeline.BuildSpellVariantsAndRegister(
                    //    gd, spellTierTable, spellBlacklist, registry,
                    //    progress: progress, cancellationToken: cts.Token
                    //);
                }, cts.Token);

                // Notify editor
                //SFCategoryManager.manual_SetGamedata();

                //MessageBox.Show(
                //    "Oblivion variant created.\nOblivion Mode."
                //);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Operation cancelled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex}");
            }
            finally
            {
                dlg.Close();
            }

            // -------------------------------------------------
            // Notify editor
            // -------------------------------------------------
            SFCategoryManager.manual_SetGamedata();

            MessageBox.Show(
                $"Oblivion variant created.\n" +
                "Oblivion Mode."
            );
        }

    }
}
