﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AuTO
{
    public partial class CreateTournamentControl : UserControl
    {
        public CreateTournamentControl()
        {
            InitializeComponent();
        }

        #region Helper Backend Functions

        /* Checks if tournament name, subdomain, url are valid and if 
         * there is at least two participants */
        public bool InitialErrorCheck()
        {
            if (string.IsNullOrWhiteSpace(nameTextbox.Text))
            {
                errorLabel.Text = "Tournament name cannot be empty.";
                errorLabel.Visible = true;
                return false;
            }
            else if (!singleRD.Checked && !doubleRD.Checked)
            {
                errorLabel.Text = "Must indicate type of tournament.";
                errorLabel.Visible = true;
                return false;
            }
            else if (string.IsNullOrWhiteSpace(urlTextbox.Text))
            {
                errorLabel.Text = "URL cannot be empty.";
                errorLabel.Visible = true;
                return false;
            }
            else if (playerListbox.Items.Count < 2)
            {
                errorLabel.Text = "Must have at least two participants.";
                errorLabel.Visible = true;
                return false;           
            }

            return true;
        }

        /* Swaps the seed numbers of two players */
        private void SwapSeeds(ref string a, ref string b)
        {
            string[] aSeed = a.Split('.');
            string[] bSeed = b.Split('.');
            string aNum = aSeed[0];
            string bNum = bSeed[0];

            aSeed[0] = bNum;
            bSeed[0] = aNum;

            a = string.Join(".", aSeed);
            b = string.Join(".", bSeed);
        }

        /* Displays error label with appropriate message */
        private void DisplayClientError(string msg)
        {
            if (!string.IsNullOrWhiteSpace(msg))
                errorLabel.Text = msg;
            else
                errorLabel.Text = "Client side error; could not start tournament.";

            errorLabel.Visible = true;
            successLabel.Visible = false;
        }

        /* Displays success label for inidicated number of seconds */
        private void DisplayClientSuccess(string msg, int seconds)
        {
            successLabel.Text = msg;
            errorLabel.Visible = false;
            successLabel.Visible = true;

            Timer t = new Timer();
            t.Interval = seconds * 1000;
            t.Tick += new EventHandler(successLabel_OnTimerEvent);
            t.Enabled = true;
        }

        #endregion

        #region Events

        private void addPlayerButton_Click(object sender, EventArgs e)
        {
            string player = playerTextbox.Text;
            int seed = playerListbox.Items.Count + 1;
   
            if (string.IsNullOrWhiteSpace(player))
            {
                errorLabel.Text = "Please input a valid name.";
                errorLabel.Visible = true;
                return;
            }

            /* Make sure player has not already been added */
            foreach (string s in playerListbox.Items)
            {
                if (s.Substring(3).Equals(player))
                {
                    errorLabel.Text = string.Format("{0} has already been added.",
                                                    playerTextbox.Text);
                    errorLabel.Visible = true;
                    return;
                }
            }
            
            /* At this point, player entry is valid; add it */
            player = string.Format("{0}. {1}", seed, player);
            playerListbox.Items.Add(player);

            playerTextbox.Clear();
            errorLabel.Visible = false;
        }

        private async void startButton_Click(object sender, EventArgs e)
        {
            /* Not enough info to start tournament; abort */
            if (!InitialErrorCheck())
                return;

            /* Set up tournament attributes */
            Tournament t = new Tournament();
            t.name = nameTextbox.Text;
            t.subdomain = subTextbox.Text;
            t.url = urlTextbox.Text;

            if (singleRD.Checked)
                t.type = "single elimination";
            else
                t.type = "double elimination";

            string[] players = new string[playerListbox.Items.Count];
            for (int k = 0; k < players.Length; k++)
            {
                /* Get rid of the"#. " at the beginning of the player's name */
                string player = playerListbox.Items[k].ToString();
                string[] components = player.Split('.');
                components[1] = components[1].Substring(1);

                players[k] = string.Join(".", components, 1, components.Length - 1 );
            }

            int validated = await Challonge.CreateTournament(t);
            if (validated < 0)
            {
                if (validated == -100)
                    DisplayClientError("Unhandled error on client side.");
                else if (validated == -200)
                    DisplayClientError("Tournament with URL already exists!");
                return;
            }

            /* Make sure tournament is retrievable */
            validated = await Challonge.GetTournament(t);
            if (validated < 0)
            {
                DisplayClientError("Tournament not found! Client side error.");
                return;
            }

            /* Add players to the tourney */
            Participant p = new Participant();
            for (int k = 0; k < players.Length; k++)
            {
                p.name = players[k];
                p.seed = k + 1;
                validated = await Challonge.AddPlayer(t, p);

                if (validated < 0)
                {
                    DisplayClientError("Could not add player; client side error.");
                    return;
                }
                else
                    DisplayClientSuccess(String.Format("Successfuilly added {0} to {1}",
                                         p.name, t.name), 3);
            }

            /* Start Tournament */
            validated = await Challonge.StartTournament(t);
            if (validated < 0)
            {
                DisplayClientError("Could not start; client side error.");
                return;
            }
            else
                DisplayClientSuccess("Tournament successfully started!", 10);
        }

        /* If user presses shift + up/down, shift the selected index up or down,
         * effectively changing the seeding. */
        private void playerListbox_KeyDown(object sender, KeyEventArgs e)
        {
            /* Only shift seeds if there's at least two entrants */
            if (playerListbox.Items.Count > 1)
            { 
                int index = 0;
                string seedToMove = playerListbox.Items[playerListbox.SelectedIndex].ToString();
                string origSeed = string.Empty;

                /* Moves higher in seed list; lower in index */
                if (e.Shift && e.KeyCode == Keys.Up)
                {
                    /* If player is already seed one, do nothing */
                    if (playerListbox.SelectedIndex == 0)
                        return;

                    /* Determine indexes and players to move */
                    index = playerListbox.SelectedIndex - 1;
                    origSeed = playerListbox.Items[index].ToString();

                    /* Swap seed numbers */
                    SwapSeeds(ref origSeed, ref seedToMove);

                    /* Move players */
                    playerListbox.Items[index] = seedToMove;
                    playerListbox.Items[index + 1] = origSeed;
                }
                /* Moves lower is seed list; higher in index */
                else if (e.Shift && e.KeyCode == Keys.Down)
                { 
                    /* If player is last seed, do nothing */
                    if (playerListbox.SelectedIndex == playerListbox.Items.Count - 1)
                        return;

                    /* Determine indexes and players to move */
                    index = playerListbox.SelectedIndex + 1;
                    origSeed = playerListbox.Items[index].ToString();

                    /* Swap seed numbers */
                    SwapSeeds(ref origSeed, ref seedToMove);

                    /* Move players */
                    playerListbox.Items[index] = seedToMove;
                    playerListbox.Items[index - 1] = origSeed;
                }
            }
        }

        private void successLabel_OnTimerEvent(object sender, EventArgs e)
        {
            successLabel.Visible = false;
        }

        /* Clear all information from wizard to start fresh*/
        private void clearButton_Click(object sender, EventArgs e)
        {
            nameTextbox.Clear();
            singleRD.Checked = false;
            doubleRD.Checked = false;
            subTextbox.Clear();
            urlTextbox.Clear();
            playerTextbox.Clear();
            playerListbox.Items.Clear();
        }

        #endregion
    }
}