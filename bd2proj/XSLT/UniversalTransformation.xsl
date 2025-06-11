<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:html="http://www.w3.org/TR/REC-html40">

    <xsl:output method="html" indent="yes"/>

    <!-- Root template -->
    <xsl:template match="/">
        <html>
            <head>
                <title>XML Viewer</title>
                <style>
                    body { font-family: monospace; white-space: pre; }
                    .element { margin-left: 20px; }
                    .attr { color: blue; }
                    .text { color: green; }
                </style>
            </head>
            <body>
                <xsl:apply-templates/>
            </body>
        </html>
    </xsl:template>

    <!-- Element handler -->
    <xsl:template match="*">
        <div class="element">
            &lt;<xsl:value-of select="name()"/>

            <!-- Attributes -->
            <xsl:for-each select="@*">
                <xsl:text> </xsl:text>
                <span class="attr">
                    <xsl:value-of select="name()"/>="<xsl:value-of select="."/>"
                </span>
            </xsl:for-each>
            &gt;

            <!-- Text content -->
            <xsl:if test="text()[normalize-space()]">
                <span class="text"><xsl:value-of select="normalize-space()"/></span>
            </xsl:if>

            <!-- Children -->
            <xsl:apply-templates/>

            &lt;/<xsl:value-of select="name()"/>&gt;
        </div>
    </xsl:template>

    <!-- Text nodes -->
    <xsl:template match="text()">
        <!-- skip whitespace-only -->
        <xsl:if test="normalize-space() != ''">
            <div class="text"><xsl:value-of select="."/></div>
        </xsl:if>
    </xsl:template>

</xsl:stylesheet>
