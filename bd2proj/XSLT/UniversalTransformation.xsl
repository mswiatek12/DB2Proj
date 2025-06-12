<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

    <xsl:output method="html" indent="yes"/>

    <xsl:template match="/">
        <html>
            <head>
                <title>XML Viewer</title>
                <style>
                    body {
                    font-family: Arial, sans-serif;
                    background-color: #f9f9f9;
                    color: #333;
                    padding: 20px;
                    }
                    .element {
                    margin-left: 20px;
                    padding-left: 10px;
                    border-left: 2px solid #ccc;
                    margin-top: 5px;
                    }
                    .tag {
                    color: #0000cc;
                    font-weight: bold;
                    }
                    .attr {
                    color: #990000;
                    margin-left: 5px;
                    }
                    .text {
                    color: #008000;
                    margin-left: 10px;
                    display: block;
                    }
                </style>
            </head>
            <body>
                <xsl:apply-templates/>
            </body>
        </html>
    </xsl:template>

    <xsl:template match="*">
        <div class="element">
            <span class="tag">&lt;<xsl:value-of select="name()"/></span>
            <xsl:for-each select="@*">
                <span class="attr">
                    <xsl:value-of select="name()"/>="<xsl:value-of select="."/>"
                </span>
            </xsl:for-each>
            <span class="tag">&gt;</span>

            <xsl:if test="text()[normalize-space()]">
                <span class="text"><xsl:value-of select="normalize-space()"/></span>
            </xsl:if>

            <xsl:apply-templates/>

            <span class="tag">&lt;/<xsl:value-of select="name()"/>&gt;</span>
        </div>
    </xsl:template>

    <xsl:template match="text()">
        <xsl:if test="normalize-space() != ''">
            <span class="text"><xsl:value-of select="."/></span>
        </xsl:if>
    </xsl:template>

</xsl:stylesheet>
