using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed partial class DeckAssemblyMatrixDialog
    {
        private static readonly string[] HeroHavens = ["Rivendell", "Lórien", "Grey Havens", "Edhellond"];

        private const string StarterIdleStatusText =
            "Total Mind limit: <= 20.\n" +
            "Starting characters prioritize: 1) drafted region group (including local Havens), 2) outside Havens, 3) any matching character.";

        private const string TheaterIdleStatusText =
            "Configure seed region, region/site counts, and terrain/hold filters, then click Run Pipeline on General Settings.";

        private Panel BuildRegionsAndSitesPage()
        {
            var tabTheater = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpDraftOpts = new GroupBox
            {
                Text = "Drafting Guidelines",
                Location = new Point(16, 16),
                Size = new Size(415, 235)
            };

            var lblSeed = new Label { Text = "Seed Region:", Location = new Point(12, 27), Size = new Size(134, 20) };
            _toolTip.SetToolTip(
                lblSeed,
                "The starting region from which the connected region group\n" +
                "expands outward (border-to-border) up to Region Count.");

            _cmbSeedRegion = new ComboBox
            {
                Location = new Point(150, 24),
                Size = new Size(252, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbSeedRegion.SelectedIndexChanged += (s, e) => UpdateSeedTooltip();

            var lblRegCount = new Label { Text = "Region Count:", Location = new Point(12, 59), Size = new Size(134, 20) };
            _numRegionCount = new NumericUpDown
            {
                Location = new Point(150, 56),
                Size = new Size(60, 23),
                Minimum = 4,
                Maximum = 12,
                Value = 5
            };
            string regCountTip =
                "Target number of connected (border-to-border) regions to draft.\n" +
                "Minimum is 4, allowing most local site-to-site moves within a single turn.";
            _toolTip.SetToolTip(lblRegCount, regCountTip);
            _toolTip.SetToolTip(_numRegionCount, regCountTip);

            var lblMaxSites = new Label { Text = "Max Adventure Sites:", Location = new Point(12, 91), Size = new Size(134, 20) };
            _numMaxSiteCount = new NumericUpDown
            {
                Location = new Point(150, 88),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 50,
                Value = 12
            };
            string maxSitesTip =
                "Maximum number of matching adventure sites (non-Haven sites) to draft.\n" +
                "Prefers a connected region group within Max; if all attempts exceed Max, keeps a random subset up to Max.";
            _toolTip.SetToolTip(lblMaxSites, maxSitesTip);
            _toolTip.SetToolTip(_numMaxSiteCount, maxSitesTip);

            var lblTheaterRulesNote = new Label
            {
                Location = new Point(12, 126),
                Size = new Size(390, 86),
                ForeColor = Color.DimGray,
                UseMnemonic = false,
                Text =
                    "Automatic Site Deck Rules:\n" +
                    "• Always drafts all 4 Havens (Rivendell, Lórien, Grey Havens, Edhellond).\n" +
                    "• Starting Company & Recruits prioritize these regions, sites, & Havens."
            };

            grpDraftOpts.Controls.AddRange([
                lblSeed, _cmbSeedRegion,
                lblRegCount, _numRegionCount,
                lblMaxSites, _numMaxSiteCount,
                lblTheaterRulesNote
            ]);

            var grpFilters = new GroupBox
            {
                Text = "Terrain && Hold Classifications (Uncheck to exclude)",
                Location = new Point(445, 16),
                Size = new Size(380, 235)
            };

            var lblRegTypes = new Label
            {
                Text = "Region Terrains:",
                Location = new Point(12, 22),
                Size = new Size(172, 16)
            };

            _clbRegionTypes = new CheckedListBox
            {
                Location = new Point(12, 42),
                Size = new Size(172, 178),
                CheckOnClick = true
            };
            _clbRegionTypes.Items.AddRange(["Wilderness", "Border-land", "Free-domain", "Shadow-land", "Dark-domain", "Coastal Sea"]);
            SetAllChecked(_clbRegionTypes, true);
            _clbRegionTypes.ItemCheck += (s, e) => BeginInvoke(ValidateWorkbenchReadiness);
            _toolTip.SetToolTip(
                _clbRegionTypes,
                "Only regions with a checked terrain type will be drafted.\n" +
                "All terrains are checked by default; uncheck any terrain you wish to avoid.");

            var lblSiteTypes = new Label
            {
                Text = "Site Holds:",
                Location = new Point(196, 22),
                Size = new Size(172, 16)
            };

            _clbSiteTypes = new CheckedListBox
            {
                Location = new Point(196, 42),
                Size = new Size(172, 178),
                CheckOnClick = true
            };
            _clbSiteTypes.Items.AddRange(["Free-hold", "Border-hold", "Ruins & Lairs", "Shadow-hold", "Dark-hold"]);
            SetAllChecked(_clbSiteTypes, true);
            _clbSiteTypes.ItemCheck += (s, e) => BeginInvoke(ValidateWorkbenchReadiness);
            _toolTip.SetToolTip(
                _clbSiteTypes,
                "Only adventure sites with a checked hold type will be drafted\n" +
                "(all 4 Havens are always included automatically).\n" +
                "Unchecking Free-hold/Border-hold will limit available recruit Home sites.");

            grpFilters.Controls.AddRange([lblRegTypes, _clbRegionTypes, lblSiteTypes, _clbSiteTypes]);

            _lblTheaterStatus = new Label
            {
                Location = new Point(16, 268),
                Size = new Size(805, 120),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = TheaterIdleStatusText
            };

            tabTheater.Controls.AddRange([grpDraftOpts, grpFilters, _lblTheaterStatus]);
            return tabTheater;
        }

        private Panel BuildStartingCompanyPage()
        {
            var tabStarter = new Panel { Dock = DockStyle.Fill, Visible = false };

            BuildCharacterGrid(tabStarter, _starterRows, 5, 42, 28, null);
            foreach (var row in _starterRows)
            {
                row.TxtMind.Text = ">=1";
            }

            _lblStarterStatus = new Label
            {
                Location = new Point(16, 198),
                Size = new Size(790, 90),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Italic),
                UseMnemonic = false,
                Text = StarterIdleStatusText
            };
            tabStarter.Controls.Add(_lblStarterStatus);

            return tabStarter;
        }

        private Panel BuildWizardsAndRecruitsPage()
        {
            var tabRecruits = new Panel { Dock = DockStyle.Fill, Visible = false };

            var grpWizards = new GroupBox
            {
                Text = "Avatar / Wizard Selection (Primary: Resources | Secondary: Sideboard or Resources)",
                Location = new Point(16, 8),
                Size = new Size(810, 82)
            };

            var lblWiz1 = new Label { Text = "Primary Wizard:", Location = new Point(12, 25), Size = new Size(105, 18) };
            _cmbWizard1 = new ComboBox
            {
                Location = new Point(120, 22),
                Size = new Size(140, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbWizard1.Items.AddRange(["(None)", "Random", "Alatar", "Gandalf", "Pallando", "Radagast", "Saruman"]);
            _cmbWizard1.SelectedIndex = 1; // Random

            var lblWiz1Cop = new Label { Text = "Copies:", Location = new Point(275, 25), Size = new Size(50, 18) };
            _cmbWizard1Copies = new ComboBox
            {
                Location = new Point(325, 22),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard1Copies.Items.AddRange(["1", "2", "3"]);
            _cmbWizard1Copies.SelectedIndex = 1; // 2 copies

            var lblWiz2 = new Label { Text = "Secondary Wizard:", Location = new Point(12, 53), Size = new Size(105, 18) };
            _cmbWizard2 = new ComboBox
            {
                Location = new Point(120, 50),
                Size = new Size(140, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2.Items.AddRange(["(None)", "Random", "Alatar", "Gandalf", "Pallando", "Radagast", "Saruman"]);
            _cmbWizard2.SelectedIndex = 1; // Random

            var lblWiz2Cop = new Label { Text = "Copies:", Location = new Point(275, 53), Size = new Size(50, 18) };
            _cmbWizard2Copies = new ComboBox
            {
                Location = new Point(325, 50),
                Size = new Size(50, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2Copies.Items.AddRange(["1", "2"]);
            _cmbWizard2Copies.SelectedIndex = 0; // 1 copy

            var lblWiz2Dest = new Label { Text = "Destination:", Location = new Point(395, 53), Size = new Size(75, 18) };
            _cmbWizard2Dest = new ComboBox
            {
                Location = new Point(475, 50),
                Size = new Size(110, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            _cmbWizard2Dest.Items.AddRange(["Sideboard", "Resources"]);
            _cmbWizard2Dest.SelectedIndex = 0;

            _cmbWizard1.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard1Copies.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2Copies.SelectedIndexChanged += WizardSelection_Changed;
            _cmbWizard2Dest.SelectedIndexChanged += WizardSelection_Changed;
            WizardSelection_Changed(null, null);

            grpWizards.Controls.AddRange([
                lblWiz1, _cmbWizard1, lblWiz1Cop, _cmbWizard1Copies,
                lblWiz2, _cmbWizard2, lblWiz2Cop, _cmbWizard2Copies,
                lblWiz2Dest, _cmbWizard2Dest
            ]);

            tabRecruits.Controls.Add(grpWizards);

            var lblRecruitsHdr = new Label
            {
                Text = "Play Deck Characters (Resources - Up to 10)",
                Location = new Point(16, 98),
                Size = new Size(400, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblRecruitsHdr);

            BuildCharacterGrid(tabRecruits, _recruitRows, 10, 138, 24, ValidateWorkbenchReadiness);

            var lblReservesHdr = new Label
            {
                Text = "Sideboard Characters (Sideboard - Up to 3)",
                Location = new Point(16, 390),
                Size = new Size(450, 18),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 54, 93)
            };
            tabRecruits.Controls.Add(lblReservesHdr);

            BuildCharacterGrid(tabRecruits, _reserveRows, 3, 430, 24, ValidateWorkbenchReadiness);

            _lblRecruitStatus = new Label
            {
                Location = new Point(16, 514),
                Size = new Size(810, 80),
                ForeColor = Color.FromArgb(74, 85, 104),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                UseMnemonic = false
            };
            tabRecruits.Controls.Add(_lblRecruitStatus);

            return tabRecruits;
        }

        private static void BuildCharacterGrid(
            Control page,
            List<CharacterRowControls> rowList,
            int count,
            int startY,
            int rowSpacing,
            Action onRowChanged)
        {
            var lblHdrSkill = new Label { Text = "Skill Focus", Location = new Point(16, startY - 20), Size = new Size(135, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrRace = new Label { Text = "Species / Race", Location = new Point(157, startY - 20), Size = new Size(135, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrMind = new Label { Text = "Mind (e.g. <=3, 2-4)", Location = new Point(298, startY - 20), Size = new Size(115, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var lblHdrDI = new Label { Text = "DI (e.g. >=2)", Location = new Point(419, startY - 20), Size = new Size(115, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };

            page.Controls.AddRange([lblHdrSkill, lblHdrRace, lblHdrMind, lblHdrDI]);

            for (int i = 0; i < count; i++)
            {
                int y = startY + (i * rowSpacing);
                var row = new CharacterRowControls
                {
                    CmbSkill = new ComboBox { Location = new Point(16, y), Size = new Size(135, 23), DropDownStyle = ComboBoxStyle.DropDownList },
                    CmbSpecies = new ComboBox { Location = new Point(157, y), Size = new Size(135, 23), DropDownStyle = ComboBoxStyle.DropDownList },
                    TxtMind = new TextBox { Location = new Point(298, y), Size = new Size(115, 23) },
                    TxtDI = new TextBox { Location = new Point(419, y), Size = new Size(115, 23) },
                    BtnClear = new Button { Text = "✕", Location = new Point(540, y - 1), Size = new Size(28, 25), ForeColor = Color.DimGray }
                };

                row.CmbSkill.Items.AddRange(["Any", "Diplomat", "Ranger", "Sage", "Scout", "Warrior"]);
                row.CmbSkill.SelectedIndex = 0;

                row.CmbSpecies.Items.AddRange(["Any", "Dwarf", "Dúnadan", "Elf", "Hobbit", "Man"]);
                row.CmbSpecies.SelectedIndex = 0;

                if (onRowChanged != null)
                {
                    row.CmbSkill.SelectedIndexChanged += (s, e) => onRowChanged();
                    row.CmbSpecies.SelectedIndexChanged += (s, e) => onRowChanged();
                    row.TxtMind.TextChanged += (s, e) => onRowChanged();
                    row.TxtDI.TextChanged += (s, e) => onRowChanged();
                }

                row.BtnClear.Click += (s, e) =>
                {
                    row.Clear();
                    onRowChanged?.Invoke();
                };

                rowList.Add(row);
                page.Controls.AddRange([row.CmbSkill, row.CmbSpecies, row.TxtMind, row.TxtDI, row.BtnClear]);
            }
        }

        private void UpdateSeedTooltip()
        {
            string sel = _cmbSeedRegion.SelectedItem?.ToString() ?? "";
            string tip = sel switch
            {
                "Random" =>
                    "Picks a random seed region anywhere across Middle-earth\n" +
                    "(respecting checked Region Terrains).",
                _ when !string.IsNullOrWhiteSpace(sel) =>
                    $"Starts expanding the connected region group outward from {sel}.",
                _ => string.Empty
            };
            _toolTip.SetToolTip(_cmbSeedRegion, tip);
        }

        private void PopulateSeedRegionDropdown()
        {
            string prev = _cmbSeedRegion.SelectedItem?.ToString() ?? "Random";
            _cmbSeedRegion.BeginUpdate();
            _cmbSeedRegion.Items.Clear();
            _cmbSeedRegion.Items.Add("Random");

            var activeSets = _getActiveSets();
            var allRegions = _catalog.Cards
                .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) &&
                            (activeSets.Count == 0 || activeSets.Contains(c.Set)))
                .Select(c => c.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase);

            foreach (var reg in allRegions)
            {
                _cmbSeedRegion.Items.Add(reg);
            }

            int idx = _cmbSeedRegion.FindStringExact(prev);
            _cmbSeedRegion.SelectedIndex = idx >= 0 ? idx : 0;
            _cmbSeedRegion.EndUpdate();
        }

        private void WizardSelection_Changed(object sender, EventArgs e)
        {
            if (_isUpdatingWizardControls) return;
            _isUpdatingWizardControls = true;

            try
            {
                bool hasWiz1 = _cmbWizard1.SelectedIndex > 0;
                _cmbWizard1Copies.Enabled = hasWiz1;
                int wiz1Copies = hasWiz1 && int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int c1) ? c1 : 0;

                bool canHaveWiz2 = hasWiz1 && wiz1Copies < 3;
                _cmbWizard2.Enabled = canHaveWiz2;

                if (!canHaveWiz2)
                {
                    _cmbWizard2.SelectedIndex = 0;
                    _cmbWizard2Copies.Enabled = false;
                    _cmbWizard2Dest.Enabled = false;
                }
                else
                {
                    string wiz1Name = _cmbWizard1.SelectedItem?.ToString() ?? "";
                    string wiz2Name = _cmbWizard2.SelectedItem?.ToString() ?? "";
                    if (_cmbWizard1.SelectedIndex > 1 && string.Equals(wiz1Name, wiz2Name, StringComparison.OrdinalIgnoreCase))
                    {
                        _cmbWizard2.SelectedIndex = 0;
                    }

                    bool hasWiz2 = _cmbWizard2.SelectedIndex > 0;
                    _cmbWizard2Copies.Enabled = hasWiz2;
                    _cmbWizard2Dest.Enabled = hasWiz2;

                    if (hasWiz2)
                    {
                        int maxWiz2Copies = Math.Min(2, 3 - wiz1Copies);
                        string prevWiz2Cop = _cmbWizard2Copies.SelectedItem?.ToString() ?? "1";
                        _cmbWizard2Copies.Items.Clear();
                        for (int i = 1; i <= maxWiz2Copies; i++)
                        {
                            _cmbWizard2Copies.Items.Add(i.ToString());
                        }
                        int idx = _cmbWizard2Copies.FindStringExact(prevWiz2Cop);
                        _cmbWizard2Copies.SelectedIndex = idx >= 0 ? idx : 0;

                        if (_cmbWizard2Dest.SelectedIndex < 0)
                        {
                            _cmbWizard2Dest.SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        _cmbWizard2Copies.Enabled = false;
                        _cmbWizard2Dest.Enabled = false;
                    }
                }
            }
            finally
            {
                _isUpdatingWizardControls = false;
            }

            ValidateWorkbenchReadiness();
        }

        private int GetSelectedWizard1Count()
        {
            if (_cmbWizard1.SelectedIndex > 0 && int.TryParse(_cmbWizard1Copies.SelectedItem?.ToString(), out int c1))
            {
                return c1;
            }
            return 0;
        }

        private int GetSelectedWizard2Count()
        {
            if (_cmbWizard2.Enabled && _cmbWizard2.SelectedIndex > 0 && int.TryParse(_cmbWizard2Copies.SelectedItem?.ToString(), out int c2))
            {
                return c2;
            }
            return 0;
        }

        private bool IsWizard2InSideboard()
        {
            return _cmbWizard2.Enabled && _cmbWizard2.SelectedIndex > 0 &&
                   string.Equals(_cmbWizard2Dest.SelectedItem?.ToString(), "Sideboard", StringComparison.OrdinalIgnoreCase);
        }

        private bool RunRegionsAndSitesDraft(bool silent = false)
        {
            var checkedTerrains = _clbRegionTypes.CheckedItems.Cast<string>().ToList();
            var checkedHolds = _clbSiteTypes.CheckedItems.Cast<string>().ToList();

            if (checkedTerrains.Count == 0 || checkedHolds.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Please select at least one Region Terrain and at least one Site Hold under Terrain & Hold Classifications.",
                        "Regions & Sites",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;
            int targetRegions = (int)_numRegionCount.Value;
            int maxSites = (int)_numMaxSiteCount.Value;

            string terrainFilter = string.Join("; ", checkedTerrains);
            string holdFilter = string.Join("; ", checkedHolds);

            var graph = _filterService.BuildRegionAdjacencyGraph(allCards);

            var allowedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var regCard in allCards.Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase)))
            {
                var rTypes = DeckGenerationService.GetRegionTypesForCard(regCard, allCards, _filterService);
                if (DeckGenerationService.MatchesRegionTypeFilter(rTypes, terrainFilter))
                {
                    allowedRegions.Add(regCard.Name.Trim());
                }
            }

            if (allowedRegions.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No regions match the checked Region Terrains.", "Regions & Sites", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            string seedChoice = _cmbSeedRegion.SelectedItem?.ToString() ?? "Random";
            bool isRandomSeed = string.Equals(seedChoice, "Random", StringComparison.OrdinalIgnoreCase);

            var candidateSeeds = allCards
                .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) &&
                            (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                            allowedRegions.Contains(c.Name.Trim()))
                .Select(c => c.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidateSeeds.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No regions in your active expansion sets match the checked Region Terrains.", "Regions & Sites", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            string terrainAdjustedNote = null;
            if (!isRandomSeed && !allowedRegions.Contains(seedChoice))
            {
                var pathToAllowed = DeckGenerationService.FindShortestRegionPath(graph, seedChoice, allowedRegions);
                if (pathToAllowed.Count == 0)
                {
                    if (!silent)
                    {
                        MessageBox.Show(
                            this,
                            $"Selected Seed Region '{seedChoice}' and all connected neighbors are excluded by the checked Region Terrains.",
                            "Regions & Sites",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    return false;
                }
                string nearestAllowed = pathToAllowed[^1];
                terrainAdjustedNote = $"Seed '{seedChoice}' excluded by terrain filter; started from nearest allowed region '{nearestAllowed}'.";
                seedChoice = nearestAllowed;
            }

            int maxAttempts = isRandomSeed ? 50 : 25;
            string bestSeed = seedChoice;
            List<string> bestCluster = [];
            int bestScore = -1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                string trialSeed = isRandomSeed
                    ? candidateSeeds[Random.Shared.Next(candidateSeeds.Count)]
                    : seedChoice;

                var trialCluster = new List<string> { trialSeed };
                ExpandClusterToTarget(graph, trialCluster, targetRegions, allowedRegions);

                int matchingSiteCount = allCards
                    .Where(c =>
                        string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                        (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                        trialCluster.Contains(c.Region, StringComparer.OrdinalIgnoreCase) &&
                        DeckGenerationService.MatchesSiteTypeFilter(c, holdFilter) &&
                        DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter))
                    .Select(c => c.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                bool meetsRegions = trialCluster.Count >= targetRegions;
                bool hasSites = matchingSiteCount > 0;
                bool withinMaxSites = hasSites && matchingSiteCount <= maxSites;

                int regionScore = meetsRegions ? 100000 : trialCluster.Count * 1000;
                int sitePresenceScore = hasSites ? 10000 : 0;
                int maxFitScore = withinMaxSites
                    ? 5000
                    : (hasSites ? Math.Max(1, 5000 - (matchingSiteCount - maxSites)) : 0);

                int score = regionScore + sitePresenceScore + maxFitScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSeed = trialSeed;
                    bestCluster = trialCluster;
                }

                if (meetsRegions && withinMaxSites)
                {
                    bestSeed = trialSeed;
                    bestCluster = trialCluster;
                    break;
                }
            }

            var draftedSitesAndRegions = new List<Card>();
            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var havenName in HeroHavens)
            {
                var havenCard = FindHeroPreferredCard(allCards, activeSets, "Site", havenName);
                if (havenCard != null && addedNames.Add(havenCard.Name))
                {
                    draftedSitesAndRegions.Add(havenCard);
                }
            }

            foreach (var regName in bestCluster)
            {
                var regCard = FindHeroPreferredCard(allCards, activeSets, "Region", regName);
                if (regCard != null && addedNames.Add(regCard.Name))
                {
                    draftedSitesAndRegions.Add(regCard);
                }
            }

            var matchingAdventureSites = allCards
                .Where(c =>
                    string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                    (activeSets.Count == 0 || activeSets.Contains(c.Set)) &&
                    bestCluster.Contains(c.Region, StringComparer.OrdinalIgnoreCase) &&
                    DeckGenerationService.MatchesSiteTypeFilter(c, holdFilter) &&
                    DeckGenerationService.MatchesRegionTypeFilter(DeckGenerationService.GetRegionTypesForCard(c, allCards, _filterService), terrainFilter))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(PickHeroPreferredFromGroup)
                .Where(c => c != null && !addedNames.Contains(c.Name))
                .ToList();

            int totalAvailableAdvSites = matchingAdventureSites.Count;
            bool wasCappedByMax = totalAvailableAdvSites > maxSites;
            IEnumerable<Card> selectedAdventureSites = wasCappedByMax
                ? matchingAdventureSites.OrderBy(_ => Random.Shared.Next()).Take(maxSites)
                : matchingAdventureSites;

            foreach (var site in selectedAdventureSites)
            {
                if (addedNames.Add(site.Name))
                {
                    draftedSitesAndRegions.Add(site);
                }
            }

            _deck.Sites.Clear();
            _deck.Sites.AddRange(draftedSitesAndRegions);
            _deck.SortSections();

            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Sites);
            }

            int finalHavens = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));
            int finalRegions = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase));
            int finalAdvSites = draftedSitesAndRegions.Count(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) && !string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase));

            string capNote = wasCappedByMax ? $" (capped from {totalAvailableAdvSites} available)" : string.Empty;
            string regionListStr = string.Join(", ", bestCluster);

            string statusMsg =
                $"Drafted {draftedSitesAndRegions.Count} cards ({finalHavens} Havens, {finalRegions} Regions, {finalAdvSites} Adventure Sites{capNote}). Seed: {bestSeed}\n" +
                $"Regions: {regionListStr}";

            var warnings = new List<string>();
            if (!string.IsNullOrEmpty(terrainAdjustedNote))
            {
                warnings.Add(terrainAdjustedNote);
            }
            if (bestCluster.Count < targetRegions)
            {
                warnings.Add($"Region Shortfall: Found only {bestCluster.Count} of {targetRegions} connected regions matching checked terrains.");
            }
            if (finalAdvSites == 0)
            {
                warnings.Add("Site Shortfall: 0 adventure sites matched the checked Site Holds inside these regions.");
            }

            if (warnings.Count > 0)
            {
                foreach (var w in warnings)
                {
                    statusMsg += $"\n⚠ {w}";
                }
                _lblTheaterStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblTheaterStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else
            {
                _lblTheaterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblTheaterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            }

            _lblTheaterStatus.Text = statusMsg;
            return true;
        }

        private bool RunStartingCompanyDraft(bool silent = false)
        {
            var activeRows = _starterRows.Where(r => r.IsActive).ToList();
            if (activeRows.Count == 0)
            {
                _lblStarterStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblStarterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
                _lblStarterStatus.Text = "No starting company rows configured (0 characters drafted into Pool).";
                return true;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var havenSites = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(c.Site, "Haven", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (draftedRegions.Count == 0 && havenSites.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Starting Company requires regions/Havens in the Site deck (run Regions & Sites first).",
                        "Starting Company",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var localHavens = havenSites
                .Where(h => !string.IsNullOrWhiteSpace(h.Region) && draftedRegions.Contains(h.Region.Trim()))
                .ToList();

            var outsideHavens = havenSites
                .Where(h => string.IsNullOrWhiteSpace(h.Region) || !draftedRegions.Contains(h.Region.Trim()))
                .ToList();

            var tier1PerRow = new List<List<Card>>(activeRows.Count);
            var tier2PerRow = new List<List<Card>>(activeRows.Count);
            var tier3PerRow = new List<List<Card>>(activeRows.Count);
            var allPerRow = new List<List<Card>>(activeRows.Count);

            for (int i = 0; i < activeRows.Count; i++)
            {
                var t1 = GetCandidatesForCharacterRow(activeRows[i], allCards, activeSets, draftedRegions, localHavens);
                var t1Ids = new HashSet<string>(t1.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);

                var t2 = outsideHavens.Count > 0
                    ? GetCandidatesForCharacterRow(activeRows[i], allCards, activeSets, null, outsideHavens)
                        .Where(c => !t1Ids.Contains(c.Id))
                        .ToList()
                    : [];

                var t12Ids = new HashSet<string>(t1Ids.Concat(t2.Select(c => c.Id)), StringComparer.OrdinalIgnoreCase);

                var t3 = GetCandidatesForCharacterRow(activeRows[i], allCards, activeSets, null, null)
                    .Where(c => !t12Ids.Contains(c.Id))
                    .ToList();

                var combined = new List<Card>(t1.Count + t2.Count + t3.Count);
                combined.AddRange(t1);
                combined.AddRange(t2);
                combined.AddRange(t3);

                if (combined.Count == 0)
                {
                    if (!silent)
                    {
                        MessageBox.Show(
                            this,
                            $"No matching characters found for Starting Company Row {i + 1} in your active expansion sets.",
                            "Starting Company",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    return false;
                }

                tier1PerRow.Add(t1);
                tier2PerRow.Add(t2);
                tier3PerRow.Add(t3);
                allPerRow.Add(combined);
            }

            var minRowMind = allPerRow.Select(list => list.Min(c => ParseCardMind(c.Mind))).ToArray();
            int minPossibleMindSum = minRowMind.Sum();

            if (minPossibleMindSum > 20)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        $"Cannot draft starting company: The minimum possible Mind sum for your selected rows is {minPossibleMindSum}, which exceeds the official 20-Mind starting pool limit.\n\n" +
                        "Please lower your Mind thresholds or clear one of the rows.",
                        "Starting Company (20-Mind Limit)",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var baseUsedTitles = new HashSet<string>(
                _deck.Resources.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)
                .Concat(_deck.Sideboard.Where(c => string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)),
                StringComparer.OrdinalIgnoreCase);

            List<Card> bestCompany = null;
            int bestScore = -1;
            int bestT1Count = 0;
            int bestT2Count = 0;
            int bestT3Count = 0;

            for (int attempt = 0; attempt < 100; attempt++)
            {
                var rowIndices = Enumerable.Range(0, activeRows.Count).ToList();
                if (attempt > 0)
                {
                    rowIndices = [.. rowIndices.OrderBy(_ => Random.Shared.Next())];
                }

                var picked = new List<Card>(activeRows.Count);
                var usedTitles = new HashSet<string>(baseUsedTitles, StringComparer.OrdinalIgnoreCase);
                int currentMind = 0;
                int remainingMinMind = minPossibleMindSum;

                int t1Count = 0;
                int t2Count = 0;
                int t3Count = 0;
                bool validPermutation = true;

                foreach (int rowIdx in rowIndices)
                {
                    remainingMinMind -= minRowMind[rowIdx];
                    int maxAllowedMind = 20 - currentMind - remainingMinMind;

                    var eligibleT1 = tier1PerRow[rowIdx]
                        .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                        .ToList();

                    Card chosen = null;
                    if (eligibleT1.Count > 0)
                    {
                        chosen = eligibleT1[Random.Shared.Next(eligibleT1.Count)];
                        t1Count++;
                    }
                    else
                    {
                        var eligibleT2 = tier2PerRow[rowIdx]
                            .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                            .ToList();

                        if (eligibleT2.Count > 0)
                        {
                            chosen = eligibleT2[Random.Shared.Next(eligibleT2.Count)];
                            t2Count++;
                        }
                        else
                        {
                            var eligibleT3 = tier3PerRow[rowIdx]
                                .Where(c => !usedTitles.Contains(c.Name) && ParseCardMind(c.Mind) <= maxAllowedMind)
                                .ToList();

                            if (eligibleT3.Count > 0)
                            {
                                chosen = eligibleT3[Random.Shared.Next(eligibleT3.Count)];
                                t3Count++;
                            }
                        }
                    }

                    if (chosen == null)
                    {
                        validPermutation = false;
                        break;
                    }

                    picked.Add(chosen);
                    usedTitles.Add(chosen.Name);
                    currentMind += ParseCardMind(chosen.Mind);
                }

                int score = (t1Count * 10000) + (t2Count * 100);
                if (validPermutation && currentMind <= 20 && score > bestScore)
                {
                    bestCompany = picked;
                    bestScore = score;
                    bestT1Count = t1Count;
                    bestT2Count = t2Count;
                    bestT3Count = t3Count;

                    if (bestT1Count == activeRows.Count)
                    {
                        break;
                    }
                }
            }

            if (bestCompany == null)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Unable to find a valid starting company combination totaling 20 Mind or less (after 100 attempts).\n\n" +
                        "Try broadening skills/species, allowing lower-mind characters (Mind 1–3), or clearing a row.",
                        "Starting Company",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            _deck.Pool.Clear();
            _deck.Pool.AddRange(bestCompany);
            _deck.SortSections();

            if (!silent)
            {
                NotifyDeckChanged(DeckSectionType.Pool);
            }

            int finalMind = bestCompany.Sum(c => ParseCardMind(c.Mind));
            string companyNames = string.Join(", ", bestCompany.Select(c => $"{c.Name} ({c.Mind} Mind)"));

            var tierParts = new List<string>();
            if (bestT1Count > 0) tierParts.Add($"{bestT1Count} local region(s)");
            if (bestT2Count > 0) tierParts.Add($"{bestT2Count} outside Haven(s)");
            if (bestT3Count > 0) tierParts.Add($"{bestT3Count} out-of-region");
            string tierNote = (bestT2Count == 0 && bestT3Count == 0)
                ? " [All from drafted regions]"
                : $" [{string.Join(", ", tierParts)}]";

            _lblStarterStatus.ForeColor = Color.FromArgb(74, 85, 104);
            _lblStarterStatus.Font = new Font("Segoe UI", 9.0f, FontStyle.Italic);
            _lblStarterStatus.Text = $"Drafted {bestCompany.Count} starting characters (Total Mind: {finalMind}/20){tierNote}:\n{companyNames}";
            return true;
        }

        private bool RunNonStartingCharactersDraft(bool silent = false)
        {
            int wiz1Count = GetSelectedWizard1Count();
            int wiz2Count = GetSelectedWizard2Count();
            var activeRecruitRows = _recruitRows.Where(r => r.IsActive).ToList();
            var activeReserveRows = _reserveRows.Where(r => r.IsActive).ToList();

            if (wiz1Count == 0 && wiz2Count == 0 && activeRecruitRows.Count == 0 && activeReserveRows.Count == 0)
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text = "No Wizards or additional characters configured (0 characters drafted).";
                return true;
            }

            if (activeRecruitRows.Count > 10)
            {
                ValidateWorkbenchReadiness();
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        $"Selection exceeds the 10-character limit for Play Deck Characters ({activeRecruitRows.Count}/10).",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            int wizInSide = IsWizard2InSideboard() ? wiz2Count : 0;
            int totalSideboardChars = activeReserveRows.Count + wizInSide;
            if (totalSideboardChars > (int)_numSideboardSize.Value)
            {
                ValidateWorkbenchReadiness();
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        $"Sideboard characters ({totalSideboardChars}) exceed the Sideboard Size limit ({(int)_numSideboardSize.Value}) set in General Settings.",
                        "Sideboard Size Limit",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var activeSets = _getActiveSets();
            var allCards = _catalog.Cards;

            var allWizards = allCards.Where(c =>
                string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Race, "Wizard", StringComparison.OrdinalIgnoreCase) &&
                (activeSets.Count == 0 || activeSets.Contains(c.Set))).ToList();

            string wiz1Choice = wiz1Count > 0 ? _cmbWizard1.SelectedItem.ToString() : string.Empty;
            string wiz2Choice = wiz2Count > 0 ? _cmbWizard2.SelectedItem.ToString() : string.Empty;

            bool isWiz1Random = string.Equals(wiz1Choice, "Random", StringComparison.OrdinalIgnoreCase);
            bool isWiz2Random = string.Equals(wiz2Choice, "Random", StringComparison.OrdinalIgnoreCase);

            if (wiz1Count > 0 && !isWiz1Random &&
                allWizards.FirstOrDefault(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz1Choice)) == null)
            {
                if (!silent)
                {
                    MessageBox.Show(this, $"Could not find Primary Wizard '{wiz1Choice}' in your active expansion sets.", "Wizards & Recruits", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            if (wiz1Count > 0 && isWiz1Random && allWizards.Count == 0)
            {
                if (!silent)
                {
                    MessageBox.Show(this, "No Wizards found in your active expansion sets.", "Wizards & Recruits", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            var draftedRegions = new HashSet<string>(
                _deck.Sites
                    .Where(c => string.Equals(c.Primary, "Region", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var allSitesInDeck = _deck.Sites
                .Where(c => string.Equals(c.Primary, "Site", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (allSitesInDeck.Count == 0 && (activeRecruitRows.Count > 0 || activeReserveRows.Count > 0))
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        "Wizards & Recruits requires Sites/Havens in the Site deck (run Regions & Sites first).",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            var tier1Sites = allSitesInDeck
                .Where(s =>
                    !string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(s.Region) && draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var tier2Sites = allSitesInDeck
                .Where(s =>
                    string.Equals(s.Site, "Haven", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(s.Region) || !draftedRegions.Contains(s.Region.Trim())))
                .ToList();

            var rowSpecs = new List<(bool IsReserve, int RowNum, List<Card> Tier1, List<Card> Tier2, List<Card> Tier3)>();

            void AddRowSpec(CharacterRowControls row, bool isReserve, int rowNum)
            {
                var t1 = tier1Sites.Count > 0
                    ? GetCandidatesForCharacterRow(row, allCards, activeSets, null, tier1Sites)
                        .Where(c => SatisfiesRecruitSiteRequirement(c, allSitesInDeck))
                        .ToList()
                    : [];
                var t1Ids = new HashSet<string>(t1.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);

                var t2 = tier2Sites.Count > 0
                    ? GetCandidatesForCharacterRow(row, allCards, activeSets, null, tier2Sites)
                        .Where(c => !t1Ids.Contains(c.Id) && SatisfiesRecruitSiteRequirement(c, allSitesInDeck))
                        .ToList()
                    : [];
                var t12Ids = new HashSet<string>(t1Ids.Concat(t2.Select(c => c.Id)), StringComparer.OrdinalIgnoreCase);

                var t3 = GetCandidatesForCharacterRow(row, allCards, activeSets, null, null)
                    .Where(c => !t12Ids.Contains(c.Id) && SatisfiesRecruitSiteRequirement(c, allSitesInDeck))
                    .ToList();

                rowSpecs.Add((isReserve, rowNum, t1, t2, t3));
            }

            for (int i = 0; i < activeRecruitRows.Count; i++)
            {
                AddRowSpec(activeRecruitRows[i], false, i + 1);
            }
            for (int i = 0; i < activeReserveRows.Count; i++)
            {
                AddRowSpec(activeReserveRows[i], true, i + 1);
            }

            var emptyRow = rowSpecs.FirstOrDefault(r => r.Tier1.Count == 0 && r.Tier2.Count == 0 && r.Tier3.Count == 0);
            if (emptyRow.Tier1 != null)
            {
                if (!silent)
                {
                    string label = emptyRow.IsReserve
                        ? $"Sideboard Character Row {emptyRow.RowNum}"
                        : $"Play Deck Character Row {emptyRow.RowNum}";
                    MessageBox.Show(
                        this,
                        $"No matching characters found for {label} whose Site Requirement is satisfied by the drafted Site deck.",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            List<Card> finalRecruits = null;
            List<Card> finalReserves = null;
            int bestScore = -1;
            int bestT1Count = 0;
            int bestT2Count = 0;
            int bestT3Count = 0;
            string lastFailureReason = null;

            for (int attempt = 0; attempt < 25; attempt++)
            {
                var attemptRecruits = new List<Card>();
                var attemptReserves = new List<Card>();
                var usedTitles = new HashSet<string>(_deck.Pool.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

                string chosenPrimaryWizardName = null;
                if (wiz1Count > 0)
                {
                    Card wiz1Card = isWiz1Random
                        ? allWizards[Random.Shared.Next(allWizards.Count)]
                        : allWizards.First(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz1Choice));

                    chosenPrimaryWizardName = wiz1Card.Name;
                    for (int i = 0; i < wiz1Count; i++) attemptRecruits.Add(wiz1Card);
                    usedTitles.Add(wiz1Card.Name);
                }

                if (wiz2Count > 0)
                {
                    var remainingWizards = allWizards
                        .Where(w => !string.Equals(w.Name, chosenPrimaryWizardName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    Card wiz2Card = isWiz2Random
                        ? (remainingWizards.Count > 0 ? remainingWizards[Random.Shared.Next(remainingWizards.Count)] : null)
                        : remainingWizards.FirstOrDefault(w => CardFilterService.EqualsWithDiacriticFallback(w.Name, wiz2Choice));

                    if (wiz2Card == null)
                    {
                        lastFailureReason = $"Could not find distinct Secondary Wizard '{wiz2Choice}' in your active expansion sets.";
                        continue;
                    }

                    bool toSideboard = IsWizard2InSideboard();
                    for (int i = 0; i < wiz2Count; i++)
                    {
                        if (toSideboard) attemptReserves.Add(wiz2Card);
                        else attemptRecruits.Add(wiz2Card);
                    }
                    usedTitles.Add(wiz2Card.Name);
                }

                var orderedSpecs = attempt == 0
                    ? [.. rowSpecs
                        .OrderBy(r => r.Tier1.Count > 0 ? 0 : (r.Tier2.Count > 0 ? 1 : 2))
                        .ThenBy(r => r.Tier1.Count > 0 ? r.Tier1.Count : (r.Tier2.Count > 0 ? r.Tier2.Count : r.Tier3.Count))]
                    : rowSpecs.OrderBy(_ => Random.Shared.Next()).ToList();

                int t1Count = 0;
                int t2Count = 0;
                int t3Count = 0;
                bool failed = false;

                foreach (var (IsReserve, RowNum, Tier1, Tier2, Tier3) in orderedSpecs)
                {
                    var availT1 = Tier1.Where(c => !usedTitles.Contains(c.Name)).ToList();
                    Card chosen = null;
                    if (availT1.Count > 0)
                    {
                        chosen = availT1[Random.Shared.Next(availT1.Count)];
                        t1Count++;
                    }
                    else
                    {
                        var availT2 = Tier2.Where(c => !usedTitles.Contains(c.Name)).ToList();
                        if (availT2.Count > 0)
                        {
                            chosen = availT2[Random.Shared.Next(availT2.Count)];
                            t2Count++;
                        }
                        else
                        {
                            var availT3 = Tier3.Where(c => !usedTitles.Contains(c.Name)).ToList();
                            if (availT3.Count > 0)
                            {
                                chosen = availT3[Random.Shared.Next(availT3.Count)];
                                t3Count++;
                            }
                        }
                    }

                    if (chosen == null)
                    {
                        string label = IsReserve ? $"Sideboard Character Row {RowNum}" : $"Play Deck Character Row {RowNum}";
                        lastFailureReason = $"Not enough distinct matching characters to fill {label}.";
                        failed = true;
                        break;
                    }

                    if (IsReserve) attemptReserves.Add(chosen);
                    else attemptRecruits.Add(chosen);

                    if (string.Equals(chosen.Unique, "Unique", StringComparison.OrdinalIgnoreCase))
                    {
                        usedTitles.Add(chosen.Name);
                    }
                }

                int score = (t1Count * 10000) + (t2Count * 100);
                if (!failed && score > bestScore)
                {
                    finalRecruits = attemptRecruits;
                    finalReserves = attemptReserves;
                    bestScore = score;
                    bestT1Count = t1Count;
                    bestT2Count = t2Count;
                    bestT3Count = t3Count;

                    if (bestT1Count == rowSpecs.Count)
                    {
                        break;
                    }
                }
            }

            if (finalRecruits == null || finalReserves == null)
            {
                if (!silent)
                {
                    MessageBox.Show(
                        this,
                        lastFailureReason ?? "Unable to draft Wizards & Recruits with current criteria.",
                        "Wizards & Recruits",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return false;
            }

            _deck.Resources.AddRange(finalRecruits);
            _deck.Sideboard.AddRange(finalReserves);
            _deck.SortSections();

            if (!silent)
            {
                NotifyDeckChanged(finalRecruits.Count > 0 ? DeckSectionType.Resources : DeckSectionType.Sideboard);
            }

            string recruitNames = finalRecruits.Count > 0 ? string.Join(", ", finalRecruits.Select(c => c.Name)) : "(None)";
            string reserveNames = finalReserves.Count > 0 ? string.Join(", ", finalReserves.Select(c => c.Name)) : "(None)";

            var tierParts = new List<string>();
            if (bestT1Count > 0) tierParts.Add($"{bestT1Count} local site(s)");
            if (bestT2Count > 0) tierParts.Add($"{bestT2Count} outside Haven(s)");
            if (bestT3Count > 0) tierParts.Add($"{bestT3Count} out-of-region");
            string tierNote = rowSpecs.Count > 0
                ? (bestT2Count == 0 && bestT3Count == 0
                    ? " [All from local sites]"
                    : $" [{string.Join(", ", tierParts)}]")
                : string.Empty;

            var wizardNotes = new List<string>();
            if (allSitesInDeck.Count > 0)
            {
                var draftedWizards = finalRecruits.Concat(finalReserves)
                    .Where(IsAvatar)
                    .Select(w => w.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var wizName in draftedWizards)
                {
                    string landSite = null;
                    if (CardFilterService.EqualsWithDiacriticFallback(wizName, "Saruman")) landSite = "Isengard";
                    else if (CardFilterService.EqualsWithDiacriticFallback(wizName, "Radagast")) landSite = "Rhosgobel";

                    if (landSite != null && !allSitesInDeck.Any(s => CardFilterService.EqualsWithDiacriticFallback(s.Name, landSite)))
                    {
                        wizardNotes.Add($"{wizName}'s home site ({landSite}) is not in the Site deck (deploys at a Haven).");
                    }
                }
            }

            if (wizardNotes.Count > 0)
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(180, 83, 9);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                _lblRecruitStatus.Text =
                    $"Drafted {finalRecruits.Count} into Resources and {finalReserves.Count} into Sideboard{tierNote}:\n" +
                    $"• Resources: {recruitNames}  |  Sideboard: {reserveNames}\n" +
                    $"⚠ {string.Join(" ", wizardNotes)}";
            }
            else
            {
                _lblRecruitStatus.ForeColor = Color.FromArgb(74, 85, 104);
                _lblRecruitStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
                _lblRecruitStatus.Text =
                    $"Drafted {finalRecruits.Count} into Resources and {finalReserves.Count} into Sideboard{tierNote}:\n" +
                    $"• Resources: {recruitNames}\n" +
                    $"• Sideboard: {reserveNames}";
            }

            return true;
        }

        private static void ExpandClusterToTarget(
            Dictionary<string, HashSet<string>> graph,
            List<string> cluster,
            int targetCount,
            HashSet<string> allowedRegions)
        {
            while (cluster.Count < targetCount)
            {
                var candidateNeighbors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var reg in cluster)
                {
                    if (graph.TryGetValue(reg, out var neighbors))
                    {
                        foreach (var n in neighbors)
                        {
                            if (!cluster.Contains(n, StringComparer.OrdinalIgnoreCase) &&
                                (allowedRegions == null || allowedRegions.Contains(n)))
                            {
                                candidateNeighbors.Add(n);
                            }
                        }
                    }
                }

                if (candidateNeighbors.Count == 0)
                {
                    break;
                }

                var candidatesList = candidateNeighbors.ToList();
                cluster.Add(candidatesList[Random.Shared.Next(candidatesList.Count)]);
            }
        }

        private static bool MatchesAnySiteInList(Card character, List<Card> siteList, CardFilterService filterService)
        {
            if (siteList == null || siteList.Count == 0 || string.IsNullOrWhiteSpace(character.Home))
            {
                return false;
            }

            string cleanHome = character.Home.Trim();
            return siteList.Any(s =>
                CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase) ||
                CardFilterService.MatchesPredicate(s, cleanHome, filterService));
        }

        private List<Card> GetCandidatesForCharacterRow(
            CharacterRowControls row,
            IReadOnlyList<Card> allCards,
            ISet<string> activeSets,
            HashSet<string> allowedRegions,
            List<Card> allowedSites)
        {
            bool checkRegions = allowedRegions != null && allowedRegions.Count > 0;
            bool checkSites = allowedSites != null && allowedSites.Count > 0;

            return [.. allCards.Where(c =>
            {
                if (!string.Equals(c.Primary, "Character", StringComparison.OrdinalIgnoreCase)) return false;
                if (activeSets.Count > 0 && !activeSets.Contains(c.Set)) return false;
                if (IsAvatar(c)) return false;

                if (row.CmbSkill.SelectedIndex > 0)
                {
                    string targetSkill = row.CmbSkill.SelectedItem.ToString();
                    if (!CardFilterService.MatchesCardnumCriterion(c, "Skill", targetSkill)) return false;
                }

                if (row.CmbSpecies.SelectedIndex > 0)
                {
                    string targetSpecies = row.CmbSpecies.SelectedItem.ToString();
                    if (!CardFilterService.MatchesCardnumCriterion(c, "Race", targetSpecies)) return false;
                }

                if (!string.IsNullOrWhiteSpace(row.TxtMind.Text))
                {
                    if (!DeckGenerationService.EvaluateNumericAttributeComparison(c, "Mind", row.TxtMind.Text.Trim())) return false;
                }

                if (!string.IsNullOrWhiteSpace(row.TxtDI.Text))
                {
                    if (!DeckGenerationService.EvaluateNumericAttributeComparison(c, "Direct", row.TxtDI.Text.Trim())) return false;
                }

                if (checkRegions || checkSites)
                {
                    bool matchedLocation = false;

                    if (checkRegions)
                    {
                        var homeRegionTags = _filterService.GetCardTagsForCategory(c.Id, "Home Region");
                        if (homeRegionTags.Any(t => allowedRegions.Any(ar => CardFilterService.EqualsWithDiacriticFallback(t.Trim(), ar))))
                        {
                            matchedLocation = true;
                        }

                        if (!matchedLocation && !string.IsNullOrWhiteSpace(c.Home))
                        {
                            string cleanHome = c.Home.Trim();
                            var homeSite = allCards.FirstOrDefault(s =>
                                string.Equals(s.Primary, "Site", StringComparison.OrdinalIgnoreCase) &&
                                (CardFilterService.EqualsWithDiacriticFallback(s.Name, cleanHome) ||
                                 string.Equals(s.Name.Replace("-", " "), cleanHome.Replace("-", " "), StringComparison.OrdinalIgnoreCase)));

                            if (homeSite != null && !string.IsNullOrWhiteSpace(homeSite.Region) &&
                                allowedRegions.Any(ar => CardFilterService.EqualsWithDiacriticFallback(homeSite.Region.Trim(), ar)))
                            {
                                matchedLocation = true;
                            }
                        }
                    }

                    if (!matchedLocation && checkSites && MatchesAnySiteInList(c, allowedSites, _filterService))
                    {
                        matchedLocation = true;
                    }

                    if (!matchedLocation) return false;
                }

                return true;
            })];
        }

        private bool SatisfiesRecruitSiteRequirement(Card character, List<Card> allSitesInDeck)
        {
            var siteReqs = _filterService.GetCardTagsForCategory(character.Id, "Site Requirement");
            if (siteReqs.Count == 0) return true;
            return siteReqs.Any(req => MatchesAnySiteRequirement(allSitesInDeck, req, _filterService));
        }
    }
}

