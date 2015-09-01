﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DES.Stuff;

using static TecWare.PPSn.Data.PpsDataHelperClient;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataTableClientDefinition ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataTableDefinitionClient : PpsDataTableDefinition
	{
		#region -- class PpsDataTableMetaCollectionClient ---------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataTableMetaCollectionClient : PpsDataTableMetaCollection
		{
			public PpsDataTableMetaCollectionClient(XElement xMetaGroup)
			{
				PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
			} // ctor
		} // class PpsDataTableMetaCollectionClient

		#endregion

		private PpsDataTableMetaCollectionClient metaInfo;

		public PpsDataTableDefinitionClient(PpsDataSetDefinitionClient dataset, XElement xTable)
			: base(dataset, xTable.GetAttribute("name", String.Empty))
		{
			ParseTable(xTable);
		} // ctor

		internal void ParseTable(XElement xTable)
		{
			foreach (XElement c in xTable.Elements())
			{
				if (c.Name == xnColumn)
					AddColumn(new PpsDataColumnDefinitionClient(this, c));
				else if (c.Name == xnRelation)
					AddColumn(new PpsDataRelationColumnClientDefinition(this, c));
				else if (c.Name == xnMeta)
					metaInfo = new PpsDataTableMetaCollectionClient(c);
				else // todo: warning
					throw new NotSupportedException(string.Format("Nicht unterstütztes Element, Name: '{0}', in der Datendefinition. \nBitte Definitionsdatei '*.sxml' korrigieren.", c.Name.LocalName));
			}
		} // func ParseTable

		internal PpsDataColumnDefinition ResolveColumn(XElement xColumn)
		{
			var tableName = xColumn.GetAttribute("table", (string)null);
			var columnName = xColumn.GetAttribute("column", (string)null);

			var table = DataSet.FindTable(tableName);
			if (table == null)
				throw new ArgumentException($"Table '{tableName}' not found.");

			var column = table.FindColumn(columnName);
			if (column == null)
				throw new ArgumentException($"Column '{columnName}' in '{tableName}' not found.");

			return column;
		} // func FindColumn

		public override PpsDataTableMetaCollection Meta { get { return metaInfo; } }
	} // class PpsDataTableClientClass

	#endregion
}
