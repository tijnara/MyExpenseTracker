using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyExpenseTracker
{
    public partial class Form2 : Form
    {
        private Form1 _form1;

        // Accept Form1 as a parameter so we can update its labels
        public Form2(Form1 form1)
        {
            InitializeComponent();
            _form1 = form1;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string input = textBox4.Text.Trim();
            if (decimal.TryParse(input, out decimal number))
            {
                try
                {
                    using (var conn = new System.Data.SQLite.SQLiteConnection("Data Source=expenses.db;Version=3;"))
                    {
                        conn.Open();
                        // Create table if it doesn't exist
                        string createTableSql = @"CREATE TABLE IF NOT EXISTS WeeklyBudget (
                                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                    Amount DECIMAL(18,2),
                                                    SetDate TEXT UNIQUE
                                                 );";
                        using (var cmd = new System.Data.SQLite.SQLiteCommand(createTableSql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                        // Calculate the Monday of the current week
                        DateTime today = DateTime.Today;
                        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                        DateTime weekStart = today.AddDays(-1 * diff).Date;

                        // Try to update first, if not exists then insert
                        string updateSql = @"UPDATE WeeklyBudget SET Amount = @Amount WHERE SetDate = @SetDate;";
                        using (var updateCmd = new System.Data.SQLite.SQLiteCommand(updateSql, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@Amount", number);
                            updateCmd.Parameters.AddWithValue("@SetDate", weekStart.ToString("yyyy-MM-dd"));
                            int rowsAffected = updateCmd.ExecuteNonQuery();

                            if (rowsAffected == 0)
                            {
                                // No row updated, insert new
                                string insertSql = @"INSERT INTO WeeklyBudget (Amount, SetDate) VALUES (@Amount, @SetDate);";
                                using (var insertCmd = new System.Data.SQLite.SQLiteCommand(insertSql, conn))
                                {
                                    insertCmd.Parameters.AddWithValue("@Amount", number);
                                    insertCmd.Parameters.AddWithValue("@SetDate", weekStart.ToString("yyyy-MM-dd"));
                                    insertCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving to database: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _form1.SetWeeklyLabels(number);
                _form1.Show();
                this.Hide();
                MessageBox.Show("The entered amount is set as the budget for the current week (Monday to Sunday).", "Weekly Budget Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Please enter a valid number.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static void ShowForm2()
        {
            Form1 form1 = new Form1();
            Form2 form2 = new Form2(form1);
            form2.Show();
        }
    }
}
