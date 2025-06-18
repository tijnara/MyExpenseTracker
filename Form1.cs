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

        public Form1()
        {
            InitializeComponent();
            button1.Click += Button1_Click;
            button2.Click += Button2_Click;

            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            InitializeDatabase();
            UpdateTotalExpenses();
            UpdateRecentExpenses();
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
                string sql = "SELECT Date, Type, Category, Amount, Notes FROM Expenses ORDER BY Date DESC LIMIT 10";
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

        private void label1_Click(object sender, EventArgs e)
        {
            // Add your desired functionality here, or leave it empty if no action is needed.
            MessageBox.Show("Label clicked!");
        }
    }
}
