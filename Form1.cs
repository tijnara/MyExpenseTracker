using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyExpenseTracker
{
    public partial class Form1 : Form
    {
        private const string DbFile = "expenses.db";
        private const string ConnectionString = "Data Source=" + DbFile + ";Version=3;";

        // Add these fields to store the weekly budget in memory
        private decimal weeklyBudget = 0;

        public Form1()
        {
            InitializeComponent();
            button1.Click += Button1_Click;
            button2.Click += Button2_Click;

            // Disable maximize
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            InitializeDatabase();
            UpdateTotalExpenses();
            UpdateRecentExpenses();
            UpdateWeeklyExpenses();
            LoadWeeklyBudget(); // Call this in the constructor after UpdateWeeklyExpenses()
        }

        private void InitializeDatabase()
        {
            if (!System.IO.File.Exists(DbFile))
            {
                SQLiteConnection.CreateFile(DbFile);
            }

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                // Expenses table
                string sqlExpenses = @"CREATE TABLE IF NOT EXISTS Expenses (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Type TEXT,
                                Category TEXT,
                                Amount REAL,
                                Date TEXT,
                                Notes TEXT
                            )";
                using (var cmd = new SQLiteCommand(sqlExpenses, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // WeeklyBudget table
                string sqlBudget = @"CREATE TABLE IF NOT EXISTS WeeklyBudget (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                WeekStart TEXT UNIQUE,
                                Amount REAL
                            )";
                using (var cmd = new SQLiteCommand(sqlBudget, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            // Check if weekly budget is set for this week
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);
            bool hasBudget = false;
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT COUNT(1) FROM WeeklyBudget WHERE WeekStart = @WeekStart";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WeekStart", monday.ToString("yyyy-MM-dd"));
                    var result = cmd.ExecuteScalar();
                    hasBudget = (result != null && Convert.ToInt32(result) > 0);
                }
            }
            if (!hasBudget)
            {
                MessageBox.Show("Please enter 'Money for this week' before adding expenses.", "Weekly Budget Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate amount
            if (!decimal.TryParse(textBox1.Text, out decimal amount) || amount < 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Determine expense type
            string type = comboBox1.Text == "Others" ? textBox2.Text : comboBox1.Text;
            if (string.IsNullOrWhiteSpace(type) || type == "Select Expense..")
            {
                MessageBox.Show("Please select or specify an expense type.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Category
            string category = comboBox2.Text;
            if (string.IsNullOrWhiteSpace(category) || category == "Select category..")
            {
                MessageBox.Show("Please select a category.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Date
            DateTime date = dateTimePicker1.Value;

            // Notes
            string notes = textBox3.Text;

            // Insert into database
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "INSERT INTO Expenses (Type, Category, Amount, Date, Notes) VALUES (@Type, @Category, @Amount, @Date, @Notes)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Type", type);
                    cmd.Parameters.AddWithValue("@Category", category);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Notes", notes);
                    cmd.ExecuteNonQuery();
                }
            }

            UpdateTotalExpenses();
            UpdateRecentExpenses();
            UpdateWeeklyExpenses();

            // Update the difference in label18 after adding the expense
            decimal weeklyBudgetValue = 0;
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT Amount FROM WeeklyBudget WHERE WeekStart = @WeekStart";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WeekStart", monday.ToString("yyyy-MM-dd"));
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        weeklyBudgetValue = Convert.ToDecimal(result);
                }
            }
            decimal totalExpenseThisWeek = 0;
            DateTime sunday = monday.AddDays(6);
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT SUM(Amount) FROM Expenses WHERE Date >= @Monday AND Date <= @Sunday";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Monday", monday.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Sunday", sunday.ToString("yyyy-MM-dd"));
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        totalExpenseThisWeek = Convert.ToDecimal(result);
                }
            }
            label18.Text = (weeklyBudgetValue - totalExpenseThisWeek).ToString("C");

            MessageBox.Show("Expense added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearForm();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            comboBox1.SelectedIndex = -1;
            comboBox1.Text = "Select Expense..";
            textBox2.Clear();
            comboBox2.SelectedIndex = -1;
            comboBox2.Text = "Select category..";
            textBox1.Clear();
            dateTimePicker1.Value = DateTime.Now;
            textBox3.Clear();
        }

        private void UpdateTotalExpenses()
        {
            var now = DateTime.Now;
            decimal total = 0;
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT SUM(Amount) FROM Expenses WHERE strftime('%m', Date) = @Month AND strftime('%Y', Date) = @Year";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Month", now.ToString("MM"));
                    cmd.Parameters.AddWithValue("@Year", now.ToString("yyyy"));
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        total = Convert.ToDecimal(result);
                }
            }
            label3.Text = total.ToString("C");
        }

        private void UpdateRecentExpenses()
        {
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();

            dataGridView1.Columns.Add("Date", "Date");
            dataGridView1.Columns.Add("Type", "Type");
            dataGridView1.Columns.Add("Category", "Category");
            dataGridView1.Columns.Add("Amount", "Amount");
            dataGridView1.Columns.Add("Notes", "Notes");

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                // Remove the LIMIT 10 to show all expenses
                string sql = "SELECT Date, Type, Category, Amount, Notes FROM Expenses ORDER BY Date DESC";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dataGridView1.Rows.Add(
                            Convert.ToDateTime(reader["Date"]).ToShortDateString(),
                            reader["Type"].ToString(),
                            reader["Category"].ToString(),
                            Convert.ToDecimal(reader["Amount"]).ToString("C"),
                            reader["Notes"].ToString()
                        );
                    }
                }
            }
        }

        private void UpdateWeeklyExpenses()
        {
            // Get the current date
            DateTime today = DateTime.Today;
            // Find the Monday of the current week
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);
            DateTime sunday = monday.AddDays(6);

            decimal weeklyTotal = 0;
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT SUM(Amount) FROM Expenses WHERE Date >= @Monday AND Date <= @Sunday";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Monday", monday.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Sunday", sunday.ToString("yyyy-MM-dd"));
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        weeklyTotal = Convert.ToDecimal(result);
                }
            }
            label14.Text = weeklyTotal.ToString("C");
        }

        private void LoadWeeklyBudget()
        {
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT Amount FROM WeeklyBudget WHERE WeekStart = @WeekStart";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@WeekStart", monday.ToString("yyyy-MM-dd"));
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                    {
                        weeklyBudget = Convert.ToDecimal(result);
                        label16.Text = weeklyBudget.ToString("C");
                        // Calculate and display the difference
                        decimal totalExpenseThisWeek = 0;
                        string label14Value = label14.Text.Replace(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, "").Trim();
                        decimal.TryParse(label14Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out totalExpenseThisWeek);
                        label18.Text = (weeklyBudget - totalExpenseThisWeek).ToString("C");
                    }
                    else
                    {
                        weeklyBudget = 0;
                        label16.Text = string.Empty;
                        label18.Text = string.Empty;
                    }
                }
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {
            // Add your desired functionality here, or leave it empty if no action is needed.
            MessageBox.Show("Label clicked!");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // Validate amount
            if (!decimal.TryParse(textBox1.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid positive number in the Amount field.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Determine expense type
            string type = comboBox1.Text == "Others" ? textBox2.Text : comboBox1.Text;
            if (string.IsNullOrWhiteSpace(type) || type == "Select Expense..")
            {
                MessageBox.Show("Please select or specify an expense type.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Category
            string category = comboBox2.Text;
            if (string.IsNullOrWhiteSpace(category) || category == "Select category..")
            {
                MessageBox.Show("Please select a category.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Date
            DateTime date = dateTimePicker1.Value;

            // Notes
            string notes = textBox3.Text;

            // Insert a negative expense entry using user input
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "INSERT INTO Expenses (Type, Category, Amount, Date, Notes) VALUES (@Type, @Category, @Amount, @Date, @Notes)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Type", type);
                    cmd.Parameters.AddWithValue("@Category", category);
                    cmd.Parameters.AddWithValue("@Amount", -amount);
                    cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Notes", notes);
                    cmd.ExecuteNonQuery();
                }
            }
            UpdateTotalExpenses();
            UpdateRecentExpenses();
            UpdateWeeklyExpenses(); // <-- Add this line
            MessageBox.Show("Amount subtracted and recorded in the database.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearForm();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            /** Get the value from textBox4 (Money This Week)
            if (!decimal.TryParse(textBox4.Text, out decimal moneyThisWeek))
            {
                MessageBox.Show("Please enter a valid number for Money This Week.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Display the value in label16
            label16.Text = moneyThisWeek.ToString("C");

            // Get the value from label14 (Total Expense This Week)
            decimal totalExpenseThisWeek = 0;
            string label14Value = label14.Text.Replace(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, "").Trim();
            decimal.TryParse(label14Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out totalExpenseThisWeek);

            // Subtract total expenses from money this week and display in label18
            decimal difference = moneyThisWeek - totalExpenseThisWeek;
            label18.Text = difference.ToString("C");
            **/
        }

        private void infoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string info = "MyExpenseTracker\n\n" +
                          "A simple expense tracking application.\n" +
                          "Developed with C# and SQLite.\n" +
                          "Track, add, and manage your expenses easily.\n\n" +
                          "© 2025 tijnara";
            MessageBox.Show(info, "About MyExpenseTracker", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            if (!decimal.TryParse(textBox4.Text, out decimal moneyThisWeek))
            {
                MessageBox.Show("Please enter a valid number for Money This Week.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);

            // Save/update the budget for this week in the WeeklyBudget table
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                // Upsert logic: delete then insert
                string deleteSql = "DELETE FROM WeeklyBudget WHERE WeekStart = @WeekStart";
                using (var deleteCmd = new SQLiteCommand(deleteSql, conn))
                {
                    deleteCmd.Parameters.AddWithValue("@WeekStart", monday.ToString("yyyy-MM-dd"));
                    deleteCmd.ExecuteNonQuery();
                }
                string insertSql = "INSERT INTO WeeklyBudget (WeekStart, Amount) VALUES (@WeekStart, @Amount)";
                using (var insertCmd = new SQLiteCommand(insertSql, conn))
                {
                    insertCmd.Parameters.AddWithValue("@WeekStart", monday.ToString("yyyy-MM-dd"));
                    insertCmd.Parameters.AddWithValue("@Amount", moneyThisWeek);
                    insertCmd.ExecuteNonQuery();
                }
            }

            // Display the value in label16
            label16.Text = moneyThisWeek.ToString("C");

            // Get the value from label14 (Total Expense This Week), but exclude "Weekly Budget" from the sum
            decimal totalExpenseThisWeek = 0;
            DateTime sunday = monday.AddDays(6);
            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string sql = "SELECT SUM(Amount) FROM Expenses WHERE Date >= @Monday AND Date <= @Sunday AND Type != 'Weekly Budget'";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Monday", monday.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Sunday", sunday.ToString("yyyy-MM-dd"));
                    var result = cmd.ExecuteScalar();
                    if (result != DBNull.Value && result != null)
                        totalExpenseThisWeek = Convert.ToDecimal(result);
                }
            }

            // Subtract total expenses from money this week and display in label18
            decimal difference = moneyThisWeek - totalExpenseThisWeek;
            label18.Text = difference.ToString("C");
        }

        private void button1_Click_1(object sender, EventArgs e)
        {

        }

        private void button2_Click_1(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {
            // Clear label16 and label18
            label16.Text = string.Empty;
            label18.Text = string.Empty;

            // Remove the weekly budget for this week from the WeeklyBudget table
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);

            using (var conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                string deleteSql = "DELETE FROM WeeklyBudget WHERE WeekStart = @WeekStart";
                using (var deleteCmd = new SQLiteCommand(deleteSql, conn))
                {
                    deleteCmd.Parameters.AddWithValue("@WeekStart", monday.ToString("yyyy-MM-dd"));
                    deleteCmd.ExecuteNonQuery();
                }
            }
        }

        private void backUpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Prompt user for backup location
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*";
                    saveFileDialog.Title = "Backup Expense Database";
                    saveFileDialog.FileName = $"expenses_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string backupPath = saveFileDialog.FileName;
                        // Ensure all data is flushed to disk before copying
                        using (var conn = new System.Data.SQLite.SQLiteConnection(ConnectionString))
                        {
                            conn.Open();
                            using (var cmd = new System.Data.SQLite.SQLiteCommand("VACUUM;", conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                        System.IO.File.Copy(DbFile, backupPath, true);
                        MessageBox.Show("Database backup completed successfully.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Backup failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void restoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
    {
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*";
            openFileDialog.Title = "Restore Expense Database";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string selectedFile = openFileDialog.FileName;

                // Close any open connections before restoring
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Overwrite the current database with the selected backup
                System.IO.File.Copy(selectedFile, DbFile, true);

                // Reload data from the restored database
                UpdateTotalExpenses();
                UpdateRecentExpenses();
                UpdateWeeklyExpenses();
                LoadWeeklyBudget();

                MessageBox.Show("Database restored successfully.", "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show("Restore failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
        }
    }
}
