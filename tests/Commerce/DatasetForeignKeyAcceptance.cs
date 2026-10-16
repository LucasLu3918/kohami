using Microsoft.Data.SqlClient;

static class DatasetForeignKeyAcceptance
{
    static readonly string[] DatasetTables =
    [
        "Documents", "Idempotency", "Inventory", "Reservations", "Collections",
        "RefundUnits", "Jobs", "Inbox", "GuestRequests", "PrivateDeliveries", "GuestCapabilities"
    ];

    static readonly (string Table, string Child, string Parent)[] CompositeForeignKeys =
    [
        ("Reservations", "Dataset,Sku", "Dataset,Sku"),
        ("RefundUnits", "Dataset,CollectionId", "Dataset,Id"),
        ("PrivateDeliveries", "Dataset,RequestId", "Dataset,Id")
    ];

    public static async Task RunAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var sku = "dataset_fk_" + suffix;
        var collection = "dataset_fk_collection_" + suffix;
        var request = "dataset_fk_request_" + suffix;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await VerifyDatasetTablesAsync(connection);
        await VerifyCompositeForeignKeyShapeAsync(connection);

        try
        {
            await VerifyParentDatasetChecksRejectAsync(connection, suffix, sku, collection, request);
            // Stage synthetic foreign parents by suspending only their Dataset CHECK constraints.
            // Composite foreign keys remain enabled throughout this acceptance.
            foreach (var table in new[] { "Inventory", "Collections", "GuestRequests" })
                await SetDatasetCheckAsync(connection, table, enabled: false);

            await ExecuteAsync(connection, "INSERT m0commerce.Inventory(Dataset,Sku,Price,Stock) VALUES('other_dataset',@sku,1,1)", ("@sku", sku));
            await ExecuteAsync(connection, "INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected) VALUES('other_dataset',@id,'synthetic','Primary',1)", ("@id", collection));
            await ExecuteAsync(connection, "INSERT m0commerce.GuestRequests(Dataset,Id,OrderId,BrowserId,Intent,NonceHash,ProofHash,ExpiresAt,Matched) VALUES('other_dataset',@id,'synthetic','synthetic','read',REPLICATE('a',64),REPLICATE('b',64),SYSUTCDATETIME(),0)", ("@id", request));

            await VerifyEveryDatasetCheckRejectsAsync(connection, suffix, sku, collection, request);
            await VerifyCrossDatasetCompositeReferencesRejectAsync(connection, suffix, sku, collection, request);
        }
        finally
        {
            await ExecuteAsync(connection,
                "DELETE m0commerce.Inventory WHERE Dataset='other_dataset' AND Sku=@sku; " +
                "DELETE m0commerce.Collections WHERE Dataset='other_dataset' AND Id=@collection; " +
                "DELETE m0commerce.GuestRequests WHERE Dataset='other_dataset' AND Id=@request",
                ("@sku", sku), ("@collection", collection), ("@request", request));
            foreach (var table in new[] { "Inventory", "Collections", "GuestRequests" })
                await SetDatasetCheckAsync(connection, table, enabled: true);
        }
    }

    static async Task VerifyDatasetTablesAsync(SqlConnection connection)
    {
        const string sql = "SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.check_constraints c ON c.parent_object_id=t.object_id WHERE s.name='m0commerce' AND c.definition LIKE '%demo_m0%' AND COL_LENGTH('m0commerce.'+t.name,'Dataset') IS NOT NULL GROUP BY t.name ORDER BY t.name";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var actual = new List<string>();
        while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
        var expected = DatasetTables.Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException($"FAILED: Dataset check table inventory; expected [{string.Join(',', expected)}], found [{string.Join(',', actual)}]");
        Console.WriteLine($"PASS: all {expected.Length} Commerce Dataset tables have the fixed demo_m0 check");
    }

    static async Task VerifyCompositeForeignKeyShapeAsync(SqlConnection connection)
    {
        const string sql = "SELECT tp.name, STRING_AGG(cp.name,',') WITHIN GROUP (ORDER BY fkc.constraint_column_id), STRING_AGG(cr.name,',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) FROM sys.foreign_keys fk JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id JOIN sys.tables tp ON tp.object_id=fk.parent_object_id JOIN sys.columns cp ON cp.object_id=fkc.parent_object_id AND cp.column_id=fkc.parent_column_id JOIN sys.columns cr ON cr.object_id=fkc.referenced_object_id AND cr.column_id=fkc.referenced_column_id JOIN sys.schemas s ON s.schema_id=tp.schema_id WHERE s.name='m0commerce' GROUP BY tp.name ORDER BY tp.name";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var actual = new Dictionary<string, (string Child, string Parent)>(StringComparer.Ordinal);
        while (await reader.ReadAsync()) actual.Add(reader.GetString(0), (reader.GetString(1), reader.GetString(2)));
        foreach (var (table, child, parent) in CompositeForeignKeys)
        {
            if (!actual.TryGetValue(table, out var columns) || columns != (child, parent))
                throw new InvalidOperationException($"FAILED: {table} Dataset composite foreign key shape; expected {child} -> {parent}");
            Console.WriteLine($"PASS: {table} foreign key contains Dataset ({child} -> {parent})");
        }
        if (actual.Count != CompositeForeignKeys.Length)
            throw new InvalidOperationException($"FAILED: unexpected Commerce foreign-key count {actual.Count}");
    }

    static async Task VerifyEveryDatasetCheckRejectsAsync(SqlConnection connection, string suffix, string sku, string collection, string request)
    {
        var other = "other_dataset";
        var statements = new (string Table, string Sql, (string Name, object Value)[] Parameters)[]
        {
            ("Documents", "INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES(@ds,'Probe',@id,'synthetic',1,'{}')", new (string, object)[] { ("@ds", other), ("@id", "d_"+suffix) }),
            ("Idempotency", "INSERT m0commerce.Idempotency(Dataset,Actor,Scope,OperationKey,RequestHash,Response,StatusCode) VALUES(@ds,'synthetic','scope',@key,REPLICATE('a',64),'{}',200)", new (string, object)[] { ("@ds", other), ("@key", "i_"+suffix) }),
            ("Reservations", "INSERT m0commerce.Reservations(Dataset,OrderId,Sku,Quantity,State,ExpiresAt) VALUES(@ds,@id,@sku,1,'Held',SYSUTCDATETIME())", new (string, object)[] { ("@ds", other), ("@id", "r_"+suffix), ("@sku", sku) }),
            ("RefundUnits", "INSERT m0commerce.RefundUnits(Dataset,UnitId,OrderId,LineId,CollectionId,Ordinal,Paid) VALUES(@ds,@id,'synthetic','line',@collection,0,1)", new (string, object)[] { ("@ds", other), ("@id", "u_"+suffix), ("@collection", collection) }),
            ("Jobs", "INSERT m0commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,ResourceKind,ResourceId,State,Payload,CreatedAt) VALUES(@ds,@id,'probe',@id,'synthetic',1,'Order',@id,'NotStarted','{}',SYSUTCDATETIME())", new (string, object)[] { ("@ds", other), ("@id", "j_"+suffix) }),
            ("Inbox", "INSERT m0commerce.Inbox(Dataset,EventId,SourceId,PayloadHash,Body) VALUES(@ds,@id,@id,REPLICATE('a',64),'{}')", new (string, object)[] { ("@ds", other), ("@id", "e_"+suffix) }),
            ("PrivateDeliveries", "INSERT m0commerce.PrivateDeliveries(Dataset,Id,RequestId,ProtectedProof) VALUES(@ds,@id,@request,'synthetic')", new (string, object)[] { ("@ds", other), ("@id", "p_"+suffix), ("@request", request) }),
            ("GuestCapabilities", "INSERT m0commerce.GuestCapabilities(Dataset,Id,OrderId,BrowserId,Intent,ExpiresAt) VALUES(@ds,@id,'synthetic','synthetic','read',SYSUTCDATETIME())", new (string, object)[] { ("@ds", other), ("@id", "g_"+suffix) })
        };

        foreach (var (table, sql, parameters) in statements)
            await ExpectSqlConstraintViolationAsync(connection, sql, parameters, $"{table} rejects a foreign Dataset discriminator");
    }

    static async Task VerifyParentDatasetChecksRejectAsync(SqlConnection connection, string suffix, string sku, string collection, string request)
    {
        await ExpectSqlConstraintViolationAsync(connection,
            "INSERT m0commerce.Inventory(Dataset,Sku,Price,Stock) VALUES('other_dataset',@sku,1,1)",
            new (string, object)[] { ("@sku", sku+"_check") }, "Inventory rejects a foreign Dataset discriminator");
        await ExpectSqlConstraintViolationAsync(connection,
            "INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected) VALUES('other_dataset',@id,'synthetic','Primary',1)",
            new (string, object)[] { ("@id", collection+"_check") }, "Collections rejects a foreign Dataset discriminator");
        await ExpectSqlConstraintViolationAsync(connection,
            "INSERT m0commerce.GuestRequests(Dataset,Id,OrderId,BrowserId,Intent,NonceHash,ProofHash,ExpiresAt,Matched) VALUES('other_dataset',@id,'synthetic','synthetic','read',REPLICATE('a',64),REPLICATE('b',64),SYSUTCDATETIME(),0)",
            new (string, object)[] { ("@id", request+"_check") }, "GuestRequests rejects a foreign Dataset discriminator");
    }

    static async Task VerifyCrossDatasetCompositeReferencesRejectAsync(SqlConnection connection, string suffix, string sku, string collection, string request)
    {
        await ExpectSqlConstraintViolationAsync(connection,
            "INSERT m0commerce.Reservations(Dataset,OrderId,Sku,Quantity,State,ExpiresAt) VALUES('demo_m0',@id,@sku,1,'Held',SYSUTCDATETIME())",
            new (string, object)[] { ("@id", "cross_r_"+suffix), ("@sku", sku) }, "Reservation cannot reference only a foreign-Dataset inventory SKU");
        await ExpectSqlConstraintViolationAsync(connection,
            "INSERT m0commerce.RefundUnits(Dataset,UnitId,OrderId,LineId,CollectionId,Ordinal,Paid) VALUES('demo_m0',@id,'synthetic','line',@collection,0,1)",
            new (string, object)[] { ("@id", "cross_u_"+suffix), ("@collection", collection) }, "RefundUnit cannot reference only a foreign-Dataset collection");
        await ExpectSqlConstraintViolationAsync(connection,
            "INSERT m0commerce.PrivateDeliveries(Dataset,Id,RequestId,ProtectedProof) VALUES('demo_m0',@id,@request,'synthetic')",
            new (string, object)[] { ("@id", "cross_p_"+suffix), ("@request", request) }, "PrivateDelivery cannot reference only a foreign-Dataset guest request");
    }

    static async Task ExpectSqlConstraintViolationAsync(SqlConnection connection, string sql, (string Name, object Value)[] parameters, string label)
    {
        try
        {
            await ExecuteAsync(connection, sql, parameters);
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            Console.WriteLine("PASS: " + label);
            return;
        }
        throw new InvalidOperationException("FAILED: " + label + " (expected SQL constraint error 547)");
    }

    static async Task SetDatasetCheckAsync(SqlConnection connection, string table, bool enabled)
    {
        var state = enabled ? "WITH CHECK CHECK" : "NOCHECK";
        const string sql = "DECLARE @table nvarchar(261)=@name, @constraint sysname; SELECT TOP(1) @constraint=name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(@table) AND definition LIKE '%demo_m0%'; IF @constraint IS NULL THROW 51000,'DATASET_CHECK_NOT_FOUND',1; DECLARE @ddl nvarchar(max)=N'ALTER TABLE '+@table+N' " + "{state}" + " CONSTRAINT '+QUOTENAME(@constraint); EXEC sys.sp_executesql @ddl";
        var commandText = sql.Replace("{state}", state, StringComparison.Ordinal);
        var fullName = "m0commerce." + table;
        await ExecuteAsync(connection, commandText, ("@name", fullName));
    }

    static async Task ExecuteAsync(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }
}
