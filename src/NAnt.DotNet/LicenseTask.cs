// NAnt - A .NET build tool
// Copyright (C) 2001-2002 Gerry Shaw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

// Matthew Mastracci (mmastrac@canada.com)

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using SourceForge.NAnt.Attributes;

namespace SourceForge.NAnt.Tasks
{
	/// <summary>
	/// Task to generate a .licence file from a .licx file.
	/// </summary>
	[TaskName("license")]
	public class LicenseTask : Task
	{
		public LicenseTask()
		{
			_assemblies = new FileSet();
		}

		protected override void ExecuteTask()
		{
			string strResourceFilename = _output;
			if ( strResourceFilename == null )
				strResourceFilename = _strTarget + ".licenses";

			if ( Verbose )
				Log.WriteLine( "Compiling {0} to {1}...", _input, strResourceFilename );

			ArrayList alAssemblies = new ArrayList();

			foreach ( string strAssembly in _assemblies.Includes )
			{
				Assembly asm;

				try
				{
					if ( File.Exists( strAssembly ) )
					{
						asm = Assembly.LoadFrom( strAssembly );
					}
					else
					{
						FileInfo fiAssembly = new FileInfo( strAssembly );
						asm = Assembly.LoadWithPartialName( Path.GetFileNameWithoutExtension( fiAssembly.Name ) );
					}

					alAssemblies.Add( asm );
				}
				catch ( Exception e )
				{
					throw new BuildException( String.Format( "Unable to load specified assembly: {0}", strAssembly ), e );
				}
			}

			StreamReader sr = new StreamReader( _input );
			DesigntimeLicenseContext dlc = new DesigntimeLicenseContext();
			LicenseManager.CurrentContext = dlc;

			Hashtable htLicenses = new Hashtable();

			while ( true )
			{
				string strLine = sr.ReadLine();
				if ( strLine == null )
					break;
				strLine = strLine.Trim();
				if ( strLine.StartsWith( "#" ) || strLine.Length == 0 || htLicenses.Contains( strLine ) )
					continue;

				if ( Verbose )
					Log.Write( strLine + ": " );
				
				string strTypeName;

				if ( strLine.IndexOf( ',' ) == -1 )
					strTypeName = strLine.Trim();
				else
					strTypeName = strLine.Split( ',' )[ 0 ];

				Type tp = null;

				foreach ( Assembly asm in alAssemblies )
				{
					tp = asm.GetType( strTypeName, false, true );
					if ( tp == null )
						continue;

					htLicenses[ strLine ] = tp;
					break;
				}

				if ( tp == null )
					throw new BuildException( String.Format( "Failed to locate type: {0}", strTypeName ), Location );

				if ( Verbose && tp != null )
					Log.WriteLine( ( ( Type )htLicenses[ strLine ] ).Assembly.CodeBase );

				if ( tp.GetCustomAttributes( typeof( LicenseProviderAttribute ), true ).Length == 0 )
					throw new BuildException( String.Format( "Type is not a licensed component: {0}", tp ), Location );

				try
				{
					LicenseManager.CreateWithContext( tp, dlc );
				}
				catch ( Exception e )
				{
					throw new BuildException( String.Format( "Failed to create license for type {0}", tp ), Location, e );
				}
			}

			if ( File.Exists( strResourceFilename ) )
			{
				File.SetAttributes( strResourceFilename, FileAttributes.Normal );
				File.Delete( strResourceFilename );
			}

			using ( FileStream fs = new FileStream( _output, FileMode.Create ) )
			{
				DesigntimeLicenseContextSerializer.Serialize( fs, _strTarget, dlc );
			}
		}

		/// <summary>Input file to process.</summary>
		[TaskAttribute("input", Required=true)]
		public string Input 
		{ 
			get { return _input; } 
			set { _input = value;} 
		}

		/// <summary>Name of the resource file to output.</summary>
		[TaskAttribute("output", Required=false)]
		public string Output 
		{ 
			get { return _output; } 
			set {_output = value;} 
		}

		/// <summary>
		/// Names of the references to scan for the licensed component.
		/// </summary>
		[FileSet("assemblies")]
		public FileSet Assemblies      
		{ 
			get { return _assemblies; } 
			set { _assemblies = value; }
		}

		/// <summary>
		/// The output executable file for which the license will be generated.
		/// </summary>
		[TaskAttribute("licensetarget", Required=true)]
		public string Target
		{
			get { return _strTarget; }
			set { _strTarget = value; }
		}

		FileSet	_assemblies;
		string _input, _output, _strTarget;
	}
}
