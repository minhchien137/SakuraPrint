-- Seed/update ZPL template cho tem LotWip (TemplateKey = 'LotWip') trong bảng
-- SM_Sakura_ZplTemplate — khớp đúng semantics SakuraService.UpsertZplTemplateAsync (update nếu
-- đã có row với TemplateKey này, insert mới nếu chưa có). Run against svn_pentaho.
-- Placeholder: {lotNumber} {productWO} {product} {lotQty} — xem ExternalPrintController.
IF EXISTS (SELECT 1 FROM dbo.SM_Sakura_ZplTemplate WHERE TemplateKey = 'LotWip')
BEGIN
    UPDATE dbo.SM_Sakura_ZplTemplate
    SET ZplContent = N'^XA
~TA000
~JSN
^LT0
^MNW
^MTT
^PON
^PMN
^LH0,0
^JMA
^PR4,4
~SD15
^JUS
^LRN
^CI27
^PA0,1,1,0
^XZ
^XA
^MMT
^PW2400
^LL3600
^LS0
^BY10,3,550^FT335,1005^BCN,550,Y,N,N,A
^FH\^FD{lotNumber}^FS
^FT243,1493^A0N,150,152^FH\^CI28^FDProduct WO: ^FS^CI27
^FT1181,1493^A0N,150,152^FH\^CI28^FD{productWO}^FS^CI27
^FT243,1815^A0N,150,152^FH\^CI28^FDProduct:^FS^CI27
^FO243,1980^A0N,133,132^FB1900,3,0,L,0^FH\^CI28^FD{product}^FS^CI27
^FT243,2612^A0N,150,152^FH\^CI28^FDLot Qty : ^FS^CI27
^FT883,2612^A0N,150,152^FH\^CI28^FD{lotQty}^FS^CI27
^PQ1,0,1,Y
^XZ
',
        UpdatedAt = GETDATE(),
        UpdatedBy = N'claude-code'
    WHERE TemplateKey = 'LotWip';
END
ELSE
BEGIN
    INSERT INTO dbo.SM_Sakura_ZplTemplate (TemplateKey, Name, ZplContent, IsActive, UpdatedAt, UpdatedBy)
    VALUES (N'LotWip', N'LotWip', N'^XA
~TA000
~JSN
^LT0
^MNW
^MTT
^PON
^PMN
^LH0,0
^JMA
^PR4,4
~SD15
^JUS
^LRN
^CI27
^PA0,1,1,0
^XZ
^XA
^MMT
^PW2400
^LL3600
^LS0
^BY10,3,550^FT335,1005^BCN,550,Y,N,N,A
^FH\^FD{lotNumber}^FS
^FT243,1493^A0N,150,152^FH\^CI28^FDProduct WO: ^FS^CI27
^FT1181,1493^A0N,150,152^FH\^CI28^FD{productWO}^FS^CI27
^FT243,1815^A0N,150,152^FH\^CI28^FDProduct:^FS^CI27
^FO243,1980^A0N,133,132^FB1900,3,0,L,0^FH\^CI28^FD{product}^FS^CI27
^FT243,2612^A0N,150,152^FH\^CI28^FDLot Qty : ^FS^CI27
^FT883,2612^A0N,150,152^FH\^CI28^FD{lotQty}^FS^CI27
^PQ1,0,1,Y
^XZ
', 1, GETDATE(), N'claude-code');
END
GO
