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
                string sql = @"CREATE TABLE IF NOT EXISTS Expenses (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Type TEXT,
                                Category TEXT,
                                Amount REAL,
                                Date TEXT,
                                Notes TEXT
                            )";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
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
            UpdateWeeklyExpenses(); // <-- Add this line
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
            // Get the current week's Monday and Sunday
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);
            DateTime sunday = monday.AddDays(6);

            // Try to load the budget for this week from a local file
            string budgetFile = "weeklybudget.txt";
            if (System.IO.File.Exists(budgetFile))
            {
                string[] lines = System.IO.File.ReadAllLines(budgetFile);
                foreach (var line in lines)
                {
                    // Format: yyyy-MM-dd|budget
                    var parts = line.Split('|');
                    if (parts.Length == 2 && DateTime.TryParse(parts[0], out DateTime weekStart))
                    {
                        if (weekStart == monday)
                        {
                            if (decimal.TryParse(parts[1], out decimal budget))
                            {
                                weeklyBudget = budget;
                                label16.Text = weeklyBudget.ToString("C");
                                // Calculate and display the difference
                                decimal totalExpenseThisWeek = 0;
                                string label14Value = label14.Text.Replace(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, "").Trim();
                                decimal.TryParse(label14Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out totalExpenseThisWeek);
                                label18.Text = (weeklyBudget - totalExpenseThisWeek).ToString("C");
                                break;
                            }
                        }
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

            // Save the budget for this week in a local file
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);
            string budgetFile = "weeklybudget.txt";
            string newLine = $"{monday:yyyy-MM-dd}|{moneyThisWeek}";

            // Remove any previous entry for this week
            var lines = System.IO.File.Exists(budgetFile) ? System.IO.File.ReadAllLines(budgetFile).ToList() : new System.Collections.Generic.List<string>();
            lines.RemoveAll(l => l.StartsWith(monday.ToString("yyyy-MM-dd") + "|"));
            lines.Add(newLine);
            System.IO.File.WriteAllLines(budgetFile, lines);

            // Display the value in label16
            label16.Text = moneyThisWeek.ToString("C");

            // Get the value from label14 (Total Expense This Week)
            decimal totalExpenseThisWeek = 0;
            string label14Value = label14.Text.Replace(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, "").Trim();
            decimal.TryParse(label14Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out totalExpenseThisWeek);

            // Subtract total expenses from money this week and display in label18
            decimal difference = moneyThisWeek - totalExpenseThisWeek;
            label18.Text = difference.ToString("C");

            // Do NOT add the weekly budget to the Expenses table
            // Only update the UI as above
        }

        private void button1_Click_1(object sender, EventArgs e)
        {

        }

        private void button2_Click_1(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {
            // Clear label16
            label16.Text = string.Empty;

            // Also clear the difference in label18
            label18.Text = string.Empty;

            // Remove the weekly budget for this week from the local file
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-1 * diff);
            string budgetFile = "weeklybudget.txt";

            if (System.IO.File.Exists(budgetFile))
            {
                var lines = System.IO.File.ReadAllLines(budgetFile).ToList();
                lines.RemoveAll(l => l.StartsWith(monday.ToString("yyyy-MM-dd") + "|"));
                System.IO.File.WriteAllLines(budgetFile, lines);
            }

        }
    }
}
