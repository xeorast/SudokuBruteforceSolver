using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SudokuSolver
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

		}

		private TextBox[][] virtualBoard;
		private readonly Brush boxBackground = new SolidColorBrush( Color.FromRgb( 0x44, 0x44, 0x44 ) );
		private readonly Brush boxForeground = new SolidColorBrush( Color.FromRgb( 0xaa, 0xaa, 0xaa ) );
		private readonly Brush userSetForeground = new SolidColorBrush( Color.FromRgb( 0xdd, 0xff, 0xdd ) );
		private readonly Brush invalidForeground = new SolidColorBrush( Color.FromRgb( 0xff, 0x66, 0x66 ) );

		private void GenerateGrid( int size )
		{
			table.RowDefinitions.Clear();
			table.ColumnDefinitions.Clear();

			int cellSize = 30;
			int fontsize = 12;
			if ( size * ( size + 1 ) * 30 > 600 )
			{
				cellSize = 600 / ( size * ( size + 1 ) );
				fontsize = (int)( 20 * ( (double)cellSize / 60 ) );
			}

			//add rows
			for ( int i = 0; i < size; i++ )
			{
				for ( int j = 0; j < size; j++ )
				{
					table.RowDefinitions.Add( new() { Height = new( cellSize ) } );
				}

				table.RowDefinitions.Add( new() { Height = new( 5 ) } );
			}

			//add columns
			for ( int i = 0; i < size; i++ )
			{
				for ( int j = 0; j < size; j++ )
				{
					table.ColumnDefinitions.Add( new() { Width = new( cellSize ) } );
				}

				table.ColumnDefinitions.Add( new() { Width = new( 5 ) } );
			}


			table.Children.Clear();
			virtualBoard = new TextBox[size * size][];
			for ( int i = 0; i < virtualBoard.Length; i++ )
			{
				virtualBoard[i] = new TextBox[size * size];
			}

			// compress 4 nested fors into one IEnumerable
			var cords = from bigCol in Enumerable.Range( 0, size )
						let colOffset = bigCol * size
						from bigRow in Enumerable.Range( 0, size )
						let rowOffset = bigRow * size
						from smallCol in Enumerable.Range( 0, size )
						from smallRow in Enumerable.Range( 0, size )
						select (bigCol, colOffset, bigRow, rowOffset, smallCol, smallRow);

			//add fields
			foreach ( var (bigCol, colOffset, bigRow, rowOffset, smallCol, smallRow) in cords )
			{
				TextBox field = new()
				{
					HorizontalContentAlignment = HorizontalAlignment.Center,
					VerticalContentAlignment = VerticalAlignment.Center,
					Background = boxBackground,
					Foreground = userSetForeground,
					FontWeight = FontWeights.Heavy,
					FontSize = fontsize
				};

				_ = table.Children.Add( field );

				Grid.SetColumn( field, colOffset + bigCol + smallCol );
				Grid.SetRow( field, rowOffset + bigRow + smallRow );

				virtualBoard[colOffset + smallCol][rowOffset + smallRow] = field;
			}
		}

		private void CreateButton_Click( object sender, RoutedEventArgs e )
		{
			threadKey++;
			if ( int.TryParse( tableSize.Text, out var size ) )
			{
				GenerateGrid( size );
			}

		}

		private int threadKey = 1;

		private void Solve( int key )
		{
			// lock fields
			changeFieldsLock( false );

			int size = virtualBoard.Length;
			int baseSize = (int)Math.Sqrt( size );

			// make int version of board
			int[][] board = new int[size][];
			for ( int i = 0; i < size; i++ )
			{
				board[i] = new int[size];
			}

			// map to ints
			Dispatcher.Invoke( () =>
			{
				for ( int column = 0; column < size; column++ )
				{
					for ( int row = 0; row < size; row++ )
					{
						if ( int.TryParse( virtualBoard[column][row].Text, out var value ) && value > 0 && value <= size )
						{
							board[column][row] = -value;
						}
					}
				}
			} );

			if ( !isUserValid() )
			{
				_ = MessageBox.Show( "invalid start data", "error", MessageBoxButton.OK );
				changeFieldsLock(false);
				return;
			}

			int visualizeFlag = 0;

			// try solve
			if ( !fillFields( board, 0, 0 ) && key == threadKey )
			{
				_ = MessageBox.Show( "sudoku unsolvable", "error", MessageBoxButton.OK );
			}

			visualize();

			// unlock fields
			changeFieldsLock(false);

			bool fillFields( int[][] fields, int col, int row )
			{
				// if got to outside of table, all fields are filled
				if ( col == size )
				{
					return true;
				}

				bool isUserSet = fields[col][row] < 0;

				// check all numbers
				for ( int i = 1; i <= size; i++ )
				{
					// abort if solving canceled
					if ( key != threadKey )
					{
						return false;
					}

					if ( isUserSet && i > 1 )
					{
						break;
					}

					// if field is not given by user, set it
					if ( !isUserSet )
					{
						fields[col][row] = i;
					}

					// if not valid, skip to not waste time
					if ( !isUserSet && !isValid( fields, col, row, baseSize ) )
					{
						continue;
					}

					//repaint to show progress
					if ( !isUserSet )
					{
						visualizeFlag++;
						if ( visualizeFlag > 10 )
						{
							visualizeFlag = 0;
							visualize();
						}
					}

					// go to next field
					if ( row == size - 1 )
					{
						if ( fillFields( fields, col + 1, 0 ) )
						{
							// all the fields after current are ok, and so is current
							return true;
						}
					}
					else
					{
						if ( fillFields( fields, col, row + 1 ) )
						{
							// all the fields after current are ok, and so is current
							return true;
						}
					}
				}
				// if no number is valid
				// reset wield so it doesnt influence previous one
				if ( !isUserSet )
				{
					fields[col][row] = 0;
				}
				// go back to previous field (increment it and start again)
				return false;
			}

			static bool isValid( int[][] fields, int col, int row, int baseSize )
			{
				int value = fields[col][row];

				// dont check zeros, they are known to repeat
				if ( value == 0 )
				{
					return true;
				}

				// user-given are negative, for comparing make them positive
				value = Math.Abs( value );

				// check row and column
				for ( int i = 0; i < fields.Length; i++ )
				{
					// check row
					if ( i != col && Math.Abs( fields[i][row] ) == value )
					{
						return false;
					}

					// check col
					if ( i != row && Math.Abs( fields[col][i] ) == value )
					{
						return false;
					}
				}

				// check square
				if ( !checkSquare( fields, col, row, baseSize ) )
				{
					return false;
				}

				return true;
			}

			static bool checkSquare( int[][] fields, int col, int row, int baseSize )
			{
				int bigCol = col / baseSize;
				int bigRow = row / baseSize;

				int colOffset = bigCol * baseSize;
				int rowOffset = bigRow * baseSize;

				int value = Math.Abs( fields[col][row] );

				int checkCol, checkRow, field;

				for ( int i = 0; i < baseSize; i++ )
				{
					checkCol = i + colOffset;
					for ( int j = 0; j < baseSize; j++ )
					{
						checkRow = j + rowOffset;

						field = Math.Abs( fields[checkCol][checkRow] );
						if ( field == value && checkCol != col && checkRow != row )
						{
							return false;
						}
					}
				}
				return true;
			}

			void visualize()
			{

				Dispatcher.Invoke( () =>
				{
					for ( int column = 0; column < size; column++ )
					{
						for ( int row = 0; row < size; row++ )
						{
							TextBox field = virtualBoard[column][row];
							int value = board[column][row];
							string textValue = value == 0 ? string.Empty : Math.Abs( value ).ToString( CultureInfo.InvariantCulture );

							field.Text = textValue;
							field.FontWeight = value < 0 ? FontWeights.Heavy : FontWeights.Thin;
							field.Foreground = value < 0 ? userSetForeground : boxForeground;
						}

					}
				}, DispatcherPriority.Normal );
			}

			void changeFieldsLock( bool @lock )
			{
				Dispatcher.Invoke( () =>
				{
					foreach ( var column in virtualBoard )
					{
						foreach ( var item in column )
						{
							item.IsReadOnly = !@lock;
						}
					}
				} );

			}

			bool isUserValid()
			{
				bool ret = true;
				for ( int i = 0; i < size; i++ )
				{
					for ( int j = 0; j < size; j++ )
					{
						if ( !isValid( board, i, j, baseSize ) )
						{
							ret = false;
							TextBox field = virtualBoard[i][j];

							Dispatcher.Invoke( () =>
							{
								field.Foreground = invalidForeground;
							}, DispatcherPriority.Normal );
						}
					}
				}
				return ret;
			}

		}

		private void SolveButton_Click( object sender, RoutedEventArgs e )
		{
			BackgroundWorker worker = new();
			worker.DoWork += new DoWorkEventHandler( worker_DoWork );
			void worker_DoWork( object sender, DoWorkEventArgs e )
			{
				Solve( threadKey );
			}
			worker.RunWorkerAsync();
		}
		private void CancelButton_Click( object sender, RoutedEventArgs e )
		{
			threadKey++;
		}
	}
}
