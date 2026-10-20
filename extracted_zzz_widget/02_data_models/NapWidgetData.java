package com.mihoyo.hyperion.biz.miniwidget.nap;

import Di.g;
import G50.l;
import G50.m;
import androidx.annotation.Keep;
import com.google.gson.annotations.SerializedName;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import x0.v;
import yA.P;

@v(parameters = 0)
@Keep
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapWidgetData {

    @l
    private static final String DEFAULT_MAIN_HOME_URL = "mihoyobbs://homeForum?game_id=8";

    @SerializedName("energy")
    @l
    private final Energy energy;

    @SerializedName("has_signed")
    private final boolean hasSigned;

    @SerializedName("home_url")
    @l
    private final String homeUrl;

    @SerializedName("hoyo_link_cn")
    @l
    private final Map<String, String> hoyoLink;

    @SerializedName("community_circle_url")
    @l
    private final String mainHomeUrl;

    @SerializedName("note_list")
    @l
    private final List<b> noteList;

    @SerializedName("note_url")
    @l
    private final String noteUrl;

    @SerializedName("sign_url")
    @l
    private final String signUrl;

    @SerializedName("vitality")
    @l
    private final Vitality vitality;

    @l
    public static final a Companion = new a(null);
    public static final int $stable = 8;

    @v(parameters = 1)
    @Keep
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class Energy {
        public static final int $stable = 0;

        @l
        private final Progress progress;
        private final int restore;

        @v(parameters = 1)
        @Keep
        /* loaded from: X:\AI project\解包\tools\classes12.dex */
        public static final class Progress {
            public static final int $stable = 0;
            private final int current;
            private final int max;

            /* JADX WARN: Illegal instructions before constructor call */
            /*
                Code decompiled incorrectly, please refer to instructions dump.
                To view partially-correct add '--show-bad-code' argument
            */
            public Progress() {
                /*
                    r3 = this;
                    r0 = 3
                    r1 = 0
                    r2 = 0
                    r3.<init>(r2, r2, r0, r1)
                    return
                */
                throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.Energy.Progress.<init>():void");
            }

            public static /* synthetic */ Progress copy$default(Progress progress, int i, int i2, int i3, Object obj) {
                if ((i3 & 1) != 0) {
                    i = progress.current;
                }
                if ((i3 & 2) != 0) {
                    i2 = progress.max;
                }
                return progress.copy(i, i2);
            }

            public final int component1() {
                return this.current;
            }

            public final int component2() {
                return this.max;
            }

            @l
            public final Progress copy(int i, int i2) {
                return new Progress(i, i2);
            }

            public boolean equals(@m Object obj) {
                if (this == obj) {
                    return true;
                }
                if (!(obj instanceof Progress)) {
                    return false;
                }
                Progress progress = (Progress) obj;
                return this.current == progress.current && this.max == progress.max;
            }

            public final int getCurrent() {
                return this.current;
            }

            public final int getMax() {
                return this.max;
            }

            public int hashCode() {
                return (Integer.hashCode(this.current) * 31) + Integer.hashCode(this.max);
            }

            @l
            public String toString() {
                return "Progress(current=" + this.current + ", max=" + this.max + ")";
            }

            public Progress(int i, int i2) {
                this.current = i;
                this.max = i2;
            }

            public /* synthetic */ Progress(int i, int i2, int i3, DefaultConstructorMarker defaultConstructorMarker) {
                this((i3 & 1) != 0 ? 0 : i, (i3 & 2) != 0 ? 0 : i2);
            }
        }

        /* JADX WARN: Illegal instructions before constructor call */
        /*
            Code decompiled incorrectly, please refer to instructions dump.
            To view partially-correct add '--show-bad-code' argument
        */
        public Energy() {
            /*
                r3 = this;
                r0 = 0
                r1 = 3
                r2 = 0
                r3.<init>(r2, r0, r1, r2)
                return
            */
            throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.Energy.<init>():void");
        }

        public static /* synthetic */ Energy copy$default(Energy energy, Progress progress, int i, int i2, Object obj) {
            if ((i2 & 1) != 0) {
                progress = energy.progress;
            }
            if ((i2 & 2) != 0) {
                i = energy.restore;
            }
            return energy.copy(progress, i);
        }

        @l
        public final Progress component1() {
            return this.progress;
        }

        public final int component2() {
            return this.restore;
        }

        @l
        public final Energy copy(@l Progress progress, int i) {
            K.p(progress, "progress");
            return new Energy(progress, i);
        }

        public boolean equals(@m Object obj) {
            if (this == obj) {
                return true;
            }
            if (!(obj instanceof Energy)) {
                return false;
            }
            Energy energy = (Energy) obj;
            return K.g(this.progress, energy.progress) && this.restore == energy.restore;
        }

        @l
        public final Progress getProgress() {
            return this.progress;
        }

        public final int getRestore() {
            return this.restore;
        }

        public int hashCode() {
            return (this.progress.hashCode() * 31) + Integer.hashCode(this.restore);
        }

        @l
        public String toString() {
            return "Energy(progress=" + this.progress + ", restore=" + this.restore + ")";
        }

        public Energy(@l Progress progress, int i) {
            K.p(progress, "progress");
            this.progress = progress;
            this.restore = i;
        }

        /* JADX WARN: Illegal instructions before constructor call */
        /*
            Code decompiled incorrectly, please refer to instructions dump.
            To view partially-correct add '--show-bad-code' argument
        */
        public /* synthetic */ Energy(com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.Energy.Progress r3, int r4, int r5, kotlin.jvm.internal.DefaultConstructorMarker r6) {
            /*
                r2 = this;
                r6 = r5 & 1
                r0 = 0
                if (r6 == 0) goto Lc
                com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Energy$Progress r3 = new com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Energy$Progress
                r6 = 3
                r1 = 0
                r3.<init>(r0, r0, r6, r1)
            Lc:
                r5 = r5 & 2
                if (r5 == 0) goto L11
                r4 = r0
            L11:
                r2.<init>(r3, r4)
                return
            */
            throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.Energy.<init>(com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Energy$Progress, int, int, kotlin.jvm.internal.DefaultConstructorMarker):void");
        }
    }

    @v(parameters = 1)
    @Keep
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class Vitality {
        public static final int $stable = 0;
        private final int current;
        private final int max;

        /* JADX WARN: Illegal instructions before constructor call */
        /*
            Code decompiled incorrectly, please refer to instructions dump.
            To view partially-correct add '--show-bad-code' argument
        */
        public Vitality() {
            /*
                r3 = this;
                r0 = 3
                r1 = 0
                r2 = 0
                r3.<init>(r2, r2, r0, r1)
                return
            */
            throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.Vitality.<init>():void");
        }

        public static /* synthetic */ Vitality copy$default(Vitality vitality, int i, int i2, int i3, Object obj) {
            if ((i3 & 1) != 0) {
                i = vitality.current;
            }
            if ((i3 & 2) != 0) {
                i2 = vitality.max;
            }
            return vitality.copy(i, i2);
        }

        public final int component1() {
            return this.current;
        }

        public final int component2() {
            return this.max;
        }

        @l
        public final Vitality copy(int i, int i2) {
            return new Vitality(i, i2);
        }

        public boolean equals(@m Object obj) {
            if (this == obj) {
                return true;
            }
            if (!(obj instanceof Vitality)) {
                return false;
            }
            Vitality vitality = (Vitality) obj;
            return this.current == vitality.current && this.max == vitality.max;
        }

        public final int getCurrent() {
            return this.current;
        }

        public final int getMax() {
            return this.max;
        }

        public int hashCode() {
            return (Integer.hashCode(this.current) * 31) + Integer.hashCode(this.max);
        }

        @l
        public String toString() {
            return "Vitality(current=" + this.current + ", max=" + this.max + ")";
        }

        public Vitality(int i, int i2) {
            this.current = i;
            this.max = i2;
        }

        public /* synthetic */ Vitality(int i, int i2, int i3, DefaultConstructorMarker defaultConstructorMarker) {
            this((i3 & 1) != 0 ? 0 : i, (i3 & 2) != 0 ? 0 : i2);
        }
    }

    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a {
        public /* synthetic */ a(DefaultConstructorMarker defaultConstructorMarker) {
            this();
        }

        public a() {
        }
    }

    @v(parameters = 1)
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class b {
        public static final int e = 0;

        @SerializedName("name")
        @l
        public final String a;

        @SerializedName("name_color")
        @l
        public final String b;

        @SerializedName("value")
        @l
        public final String c;

        @SerializedName("value_color")
        @l
        public final String d;

        public b(@l String str, @l String str2, @l String str3, @l String str4) {
            K.p(str, "name");
            K.p(str2, "nameColor");
            K.p(str3, "value");
            K.p(str4, "valueColor");
            this.a = str;
            this.b = str2;
            this.c = str3;
            this.d = str4;
        }

        public static /* synthetic */ b f(b bVar, String str, String str2, String str3, String str4, int i, Object obj) {
            if ((i & 1) != 0) {
                str = bVar.a;
            }
            if ((i & 2) != 0) {
                str2 = bVar.b;
            }
            if ((i & 4) != 0) {
                str3 = bVar.c;
            }
            if ((i & 8) != 0) {
                str4 = bVar.d;
            }
            return bVar.e(str, str2, str3, str4);
        }

        @l
        public final String a() {
            return this.a;
        }

        @l
        public final String b() {
            return this.b;
        }

        @l
        public final String c() {
            return this.c;
        }

        @l
        public final String d() {
            return this.d;
        }

        @l
        public final b e(@l String str, @l String str2, @l String str3, @l String str4) {
            K.p(str, "name");
            K.p(str2, "nameColor");
            K.p(str3, "value");
            K.p(str4, "valueColor");
            return new b(str, str2, str3, str4);
        }

        public boolean equals(@m Object obj) {
            if (this == obj) {
                return true;
            }
            if (!(obj instanceof b)) {
                return false;
            }
            b bVar = (b) obj;
            return K.g(this.a, bVar.a) && K.g(this.b, bVar.b) && K.g(this.c, bVar.c) && K.g(this.d, bVar.d);
        }

        @l
        public final String g() {
            return this.a;
        }

        @l
        public final String h() {
            return this.b;
        }

        public int hashCode() {
            return (((((this.a.hashCode() * 31) + this.b.hashCode()) * 31) + this.c.hashCode()) * 31) + this.d.hashCode();
        }

        @l
        public final String i() {
            return this.c;
        }

        @l
        public final String j() {
            return this.d;
        }

        @l
        public String toString() {
            return "Note(name=" + this.a + ", nameColor=" + this.b + ", value=" + this.c + ", valueColor=" + this.d + ")";
        }
    }

    public NapWidgetData() {
        this(null, false, null, null, null, null, null, null, null, 511, null);
    }

    @l
    public final Energy component1() {
        return this.energy;
    }

    public final boolean component2() {
        return this.hasSigned;
    }

    @l
    public final String component3() {
        return this.homeUrl;
    }

    @l
    public final String component4() {
        return this.noteUrl;
    }

    @l
    public final String component5() {
        return this.signUrl;
    }

    @l
    public final Vitality component6() {
        return this.vitality;
    }

    @l
    public final List<b> component7() {
        return this.noteList;
    }

    @l
    public final Map<String, String> component8() {
        return this.hoyoLink;
    }

    @l
    public final String component9() {
        return this.mainHomeUrl;
    }

    @l
    public final NapWidgetData copy(@l Energy energy, boolean z, @l String str, @l String str2, @l String str3, @l Vitality vitality, @l List<b> list, @l Map<String, String> map, @l String str4) {
        K.p(energy, "energy");
        K.p(str, "homeUrl");
        K.p(str2, "noteUrl");
        K.p(str3, "signUrl");
        K.p(vitality, "vitality");
        K.p(list, "noteList");
        K.p(map, "hoyoLink");
        K.p(str4, "mainHomeUrl");
        return new NapWidgetData(energy, z, str, str2, str3, vitality, list, map, str4);
    }

    public boolean equals(@m Object obj) {
        if (this == obj) {
            return true;
        }
        if (!(obj instanceof NapWidgetData)) {
            return false;
        }
        NapWidgetData napWidgetData = (NapWidgetData) obj;
        return K.g(this.energy, napWidgetData.energy) && this.hasSigned == napWidgetData.hasSigned && K.g(this.homeUrl, napWidgetData.homeUrl) && K.g(this.noteUrl, napWidgetData.noteUrl) && K.g(this.signUrl, napWidgetData.signUrl) && K.g(this.vitality, napWidgetData.vitality) && K.g(this.noteList, napWidgetData.noteList) && K.g(this.hoyoLink, napWidgetData.hoyoLink) && K.g(this.mainHomeUrl, napWidgetData.mainHomeUrl);
    }

    @l
    public final Energy getEnergy() {
        return this.energy;
    }

    public final boolean getHasSigned() {
        return this.hasSigned;
    }

    @l
    public final String getHomeUrl() {
        return this.homeUrl;
    }

    @l
    public final Map<String, String> getHoyoLink() {
        return this.hoyoLink;
    }

    @l
    public final String getMainHomeUrl() {
        return this.mainHomeUrl;
    }

    @l
    public final List<b> getNoteList() {
        return this.noteList;
    }

    @l
    public final String getNoteUrl() {
        return this.noteUrl;
    }

    @l
    public final List<g> getPlayLinks() {
        Map<String, String> map = this.hoyoLink;
        g.a aVar = g.a.DEEPLINK;
        String str = map.get(aVar.getValue());
        Map<String, String> map2 = this.hoyoLink;
        g.a aVar2 = g.a.DEEPLINK_CLOUD;
        String str2 = map2.get(aVar2.getValue());
        Map<String, String> map3 = this.hoyoLink;
        g.a aVar3 = g.a.APP_LINK;
        String str3 = map3.get(aVar3.getValue());
        ArrayList arrayList = new ArrayList();
        if (str != null && !P.O3(str)) {
            arrayList.add(new g(aVar.getValue(), str));
        }
        if (str2 != null && !P.O3(str2)) {
            arrayList.add(new g(aVar2.getValue(), str2));
        }
        if (str3 != null && !P.O3(str3)) {
            arrayList.add(new g(aVar3.getValue(), str3));
        }
        return arrayList;
    }

    @l
    public final String getSignUrl() {
        return this.signUrl;
    }

    @l
    public final Vitality getVitality() {
        return this.vitality;
    }

    public int hashCode() {
        return (((((((((((((((this.energy.hashCode() * 31) + Boolean.hashCode(this.hasSigned)) * 31) + this.homeUrl.hashCode()) * 31) + this.noteUrl.hashCode()) * 31) + this.signUrl.hashCode()) * 31) + this.vitality.hashCode()) * 31) + this.noteList.hashCode()) * 31) + this.hoyoLink.hashCode()) * 31) + this.mainHomeUrl.hashCode();
    }

    @l
    public String toString() {
        return "NapWidgetData(energy=" + this.energy + ", hasSigned=" + this.hasSigned + ", homeUrl=" + this.homeUrl + ", noteUrl=" + this.noteUrl + ", signUrl=" + this.signUrl + ", vitality=" + this.vitality + ", noteList=" + this.noteList + ", hoyoLink=" + this.hoyoLink + ", mainHomeUrl=" + this.mainHomeUrl + ")";
    }

    public NapWidgetData(@l Energy energy, boolean z, @l String str, @l String str2, @l String str3, @l Vitality vitality, @l List<b> list, @l Map<String, String> map, @l String str4) {
        K.p(energy, "energy");
        K.p(str, "homeUrl");
        K.p(str2, "noteUrl");
        K.p(str3, "signUrl");
        K.p(vitality, "vitality");
        K.p(list, "noteList");
        K.p(map, "hoyoLink");
        K.p(str4, "mainHomeUrl");
        this.energy = energy;
        this.hasSigned = z;
        this.homeUrl = str;
        this.noteUrl = str2;
        this.signUrl = str3;
        this.vitality = vitality;
        this.noteList = list;
        this.hoyoLink = map;
        this.mainHomeUrl = str4;
    }

    /* JADX WARN: Illegal instructions before constructor call */
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public /* synthetic */ NapWidgetData(com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.Energy r11, boolean r12, java.lang.String r13, java.lang.String r14, java.lang.String r15, com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.Vitality r16, java.util.List r17, java.util.Map r18, java.lang.String r19, int r20, kotlin.jvm.internal.DefaultConstructorMarker r21) {
        /*
            r10 = this;
            r0 = r20
            r1 = r0 & 1
            r2 = 3
            r3 = 0
            r4 = 0
            if (r1 == 0) goto Lf
            com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Energy r1 = new com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Energy
            r1.<init>(r3, r4, r2, r3)
            goto L10
        Lf:
            r1 = r11
        L10:
            r5 = r0 & 2
            if (r5 == 0) goto L16
            r5 = r4
            goto L17
        L16:
            r5 = r12
        L17:
            r6 = r0 & 4
            java.lang.String r7 = ""
            if (r6 == 0) goto L1f
            r6 = r7
            goto L20
        L1f:
            r6 = r13
        L20:
            r8 = r0 & 8
            if (r8 == 0) goto L26
            r8 = r7
            goto L27
        L26:
            r8 = r14
        L27:
            r9 = r0 & 16
            if (r9 == 0) goto L2c
            goto L2d
        L2c:
            r7 = r15
        L2d:
            r9 = r0 & 32
            if (r9 == 0) goto L37
            com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Vitality r9 = new com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Vitality
            r9.<init>(r4, r4, r2, r3)
            goto L39
        L37:
            r9 = r16
        L39:
            r2 = r0 & 64
            if (r2 == 0) goto L42
            java.util.List r2 = Cy.H.H()
            goto L44
        L42:
            r2 = r17
        L44:
            r3 = r0 & 128(0x80, float:1.8E-43)
            if (r3 == 0) goto L4d
            java.util.Map r3 = Cy.o0.z()
            goto L4f
        L4d:
            r3 = r18
        L4f:
            r0 = r0 & 256(0x100, float:3.59E-43)
            if (r0 == 0) goto L56
            java.lang.String r0 = "mihoyobbs://homeForum?game_id=8"
            goto L58
        L56:
            r0 = r19
        L58:
            r11 = r10
            r12 = r1
            r13 = r5
            r14 = r6
            r15 = r8
            r16 = r7
            r17 = r9
            r18 = r2
            r19 = r3
            r20 = r0
            r11.<init>(r12, r13, r14, r15, r16, r17, r18, r19, r20)
            return
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData.<init>(com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Energy, boolean, java.lang.String, java.lang.String, java.lang.String, com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData$Vitality, java.util.List, java.util.Map, java.lang.String, int, kotlin.jvm.internal.DefaultConstructorMarker):void");
    }
}
