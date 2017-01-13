﻿#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

using static TecWare.PPSn.Data.PpsDataHelperClient;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataSetDefinitionClient -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetDefinitionClient : PpsDataSetDefinition
	{
		#region -- class PpsDataSetMetaCollectionClient -----------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetMetaCollectionClient : PpsDataSetMetaCollection
		{
			public PpsDataSetMetaCollectionClient(XElement xMetaGroup)
			{
				PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
			} // ctor
		} // class PpsDataSetMetaCollectionClient

		#endregion

		private readonly IPpsShell shell;
		private readonly string schema;
		private PpsDataSetMetaCollectionClient metaInfo;

		public PpsDataSetDefinitionClient(IPpsShell shell, string schema, XElement xSchema)
		{
			this.shell = shell;
			this.schema = schema;

			// read definitions
			foreach (XElement c in xSchema.Elements())
			{
				if (c.Name == xnTable)
					Add(CreateDataTable(c));
				else if (c.Name == xnTag)
					Add(CreateAutoTagDefinition(c));
				else if (c.Name == xnMeta)
					metaInfo = new PpsDataSetMetaCollectionClient(c);
			}

			// create always a meta data collection
			if (metaInfo == null)
				metaInfo = new PpsDataSetMetaCollectionClient(new XElement("meta"));
		} // ctor

		protected virtual PpsDataTableDefinitionClient CreateDataTable(XElement c)
			=> new PpsDataTableDefinitionClient(this, c);
		

		private PpsDataSetAutoTagDefinition CreateAutoTagDefinition(XElement x)
		{
			var tagName = x.GetAttribute("name", String.Empty);
			var tagMode = x.GetAttribute("mode", PpsDataSetAutoTagMode.First);
			var tableName = x.GetAttribute("tableName", String.Empty);
			var columnName = x.GetAttribute("columnName", String.Empty);

			return new PpsDataSetAutoTagDefinition(this, tagName, tableName, columnName, tagMode);
		} // func CreateAutoTagDefinition

		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetClient(this, shell);

		public string SchemaType => schema;

		public IPpsShell Shell => shell;

		public override PpsDataSetMetaCollection Meta => metaInfo;
	} // class PpsDataSetDefinitionClient

	#endregion

	#region -- class PpsDataSetClient ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetClient : PpsDataSet, INotifyPropertyChanged
	{
		#region -- class PpsDataSetTable --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetTable : LuaTable
		{
			private readonly PpsDataSetClient document;

			public PpsDataSetTable(PpsDataSetClient document)
			{
				this.document = document;
			} // ctor

			private object GetDocumentTable(string key)
			{
				return null;
			} // func GetDocumentTable

			protected override object OnIndex(object key)
			{
				return base.OnIndex(key) ??
					GetDocumentTable(key as string) ??
					document.shell.LuaLibrary.GetValue(key);
			} // func OnIndex

			[LuaMember(nameof(Arguments))]
			public LuaTable Arguments => document.arguments;
		} // class PpsDocumentTable

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		private LuaTable arguments;
		private readonly IPpsShell shell;

		private bool isDirty = false;             // is this document changed since the last dump

		protected internal PpsDataSetClient(PpsDataSetDefinition datasetDefinition, IPpsShell shell)
			: base(datasetDefinition)
		{
			this.shell = shell;
		} // ctor

		protected override object GetEnvironmentValue(object key)
			=> shell.LuaLibrary?.GetValue(key);

		#region -- Dirty Flag -------------------------------------------------------------

		private void SetDirty()
		{
			if (!isDirty)
			{
				isDirty = true;
				OnPropertyChanged(nameof(IsDirty));
			}
		} // proc SetDirty

		public void ResetDirty()
		{
			if (!isDirty)
			{
				isDirty = false;
				OnPropertyChanged(nameof(IsDirty));
			}
		} // proc ResetDirty

		protected override void OnDataChanged()
		{
			base.OnDataChanged();
			SetDirty();
		} // proc OnDataChanged

		#endregion
		
		/// <summary>Initialize a new dataset</summary>
		public virtual async Task OnNewAsync(LuaTable arguments)
		{
			this.arguments = arguments;

			// call initalization hook
			using (var trans = UndoSink?.BeginTransaction("Init"))
			{
				// create head
				var head = Tables["Head", true];
				var row = head.Add();
				row["Typ"] = ((PpsDataSetDefinitionClient)DataSetDefinition).SchemaType;
				row["Guid"] = Guid.NewGuid();

				await InvokeEventHandlerAsync("OnNewAsync");
				trans?.Commit();
			}
		} // proc OnNewAsync

		public virtual async Task OnLoadedAsync(LuaTable arguments)
		{
			this.arguments = arguments;

			// call initalization hook
			using (var trans = UndoSink?.BeginTransaction("Init"))
			{
				await InvokeEventHandlerAsync("OnLoadedAsync");
				trans?.Commit();
			}
		} // proc OnLoadedAsync

		protected void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>Is the dataset initialized.</summary>
		public bool IsInitialized => arguments != null;

		/// <summary>Environment of the dataset.</summary>
		public IPpsShell Shell => shell;
		/// <summary>Is the current dataset changed.</summary>
		public bool IsDirty => isDirty;
	} // class PpsDataSetClient

	#endregion
}
