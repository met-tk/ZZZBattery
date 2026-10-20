package com.mihoyo.hyperion.biz.miniwidget.napavatar;

import Cy.H;
import G50.l;
import G50.m;
import android.annotation.SuppressLint;
import android.graphics.Color;
import androidx.annotation.Keep;
import com.google.gson.annotations.SerializedName;
import java.util.List;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import pg.InterfaceC0771a;
import x0.v;
import yA.P;

@v(parameters = 0)
@Keep
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class AvatarBackground {
    public static final int $stable = 8;

    @SerializedName("avatar_profession_list")
    @l
    private final List<b> avatarProfessions;

    @SerializedName("avatar_skin_list_v2")
    @l
    private final List<a> avatarSkins;

    @SerializedName("element_type_list")
    @l
    private final List<b> elementTypes;

    @v(parameters = 0)
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a implements InterfaceC0771a {
        public static final int i = 8;

        @SerializedName("avatar_id")
        @l
        public final String a;

        @SerializedName("avatar_profession")
        @l
        public final String b;

        @SerializedName("element_type")
        @l
        public final String c;

        @SerializedName("skin_background_url")
        @l
        public final String d;

        @SerializedName("skin_name")
        @l
        public final String e;

        @SerializedName("color")
        @l
        public final String f;

        @SerializedName("skin_id")
        @l
        public final String g;
        public transient boolean h;

        public a() {
            this(null, null, null, null, null, null, null, 127, null);
        }

        public static /* synthetic */ a n(a aVar, String str, String str2, String str3, String str4, String str5, String str6, String str7, int i2, Object obj) {
            if ((i2 & 1) != 0) {
                str = aVar.a;
            }
            if ((i2 & 2) != 0) {
                str2 = aVar.b;
            }
            String str8 = str2;
            if ((i2 & 4) != 0) {
                str3 = aVar.c;
            }
            String str9 = str3;
            if ((i2 & 8) != 0) {
                str4 = aVar.d;
            }
            String str10 = str4;
            if ((i2 & 16) != 0) {
                str5 = aVar.e;
            }
            String str11 = str5;
            if ((i2 & 32) != 0) {
                str6 = aVar.f;
            }
            String str12 = str6;
            if ((i2 & 64) != 0) {
                str7 = aVar.g;
            }
            return aVar.m(str, str8, str9, str10, str11, str12, str7);
        }

        @Override // pg.InterfaceC0771a
        public void a(boolean z) {
            this.h = z;
        }

        @Override // pg.InterfaceC0771a
        @l
        public String b() {
            return this.f;
        }

        @Override // pg.InterfaceC0771a
        @l
        public String d() {
            return this.e;
        }

        @Override // pg.InterfaceC0771a
        public boolean e() {
            return this.h;
        }

        public boolean equals(@m Object obj) {
            if (this == obj) {
                return true;
            }
            if (!(obj instanceof a)) {
                return false;
            }
            a aVar = (a) obj;
            return K.g(this.a, aVar.a) && K.g(this.b, aVar.b) && K.g(this.c, aVar.c) && K.g(this.d, aVar.d) && K.g(this.e, aVar.e) && K.g(this.f, aVar.f) && K.g(this.g, aVar.g);
        }

        public final String f() {
            return this.a;
        }

        @l
        public final String g() {
            return this.b;
        }

        @Override // pg.InterfaceC0771a
        @l
        public String getId() {
            return this.a + "_" + this.g;
        }

        @l
        public final String h() {
            return this.c;
        }

        public int hashCode() {
            return (((((((((((this.a.hashCode() * 31) + this.b.hashCode()) * 31) + this.c.hashCode()) * 31) + this.d.hashCode()) * 31) + this.e.hashCode()) * 31) + this.f.hashCode()) * 31) + this.g.hashCode();
        }

        @l
        public final String i() {
            return this.d;
        }

        @l
        public final String j() {
            return this.e;
        }

        @l
        public final String k() {
            return this.f;
        }

        public final String l() {
            return this.g;
        }

        @l
        public final a m(@l String str, @l String str2, @l String str3, @l String str4, @l String str5, @l String str6, @l String str7) {
            K.p(str, "avatarId");
            K.p(str2, "avatarProfession");
            K.p(str3, "elementType");
            K.p(str4, "res");
            K.p(str5, "itemName");
            K.p(str6, "coverColorValue");
            K.p(str7, "skinId");
            return new a(str, str2, str3, str4, str5, str6, str7);
        }

        @l
        public final String o() {
            return this.b;
        }

        @l
        public final String p() {
            return this.c;
        }

        @Override // pg.InterfaceC0771a
        @l
        /* renamed from: q, reason: merged with bridge method [inline-methods] */
        public String c() {
            return this.d;
        }

        @SuppressLint({"Range"})
        public final boolean r() {
            boolean z;
            try {
                Color.parseColor(b());
                z = true;
            } catch (Exception unused) {
                z = false;
            }
            return (P.O3(this.a) || P.O3(this.g) || P.O3(c()) || P.O3(d()) || P.O3(b()) || !z) ? false : true;
        }

        @l
        public String toString() {
            return "AvatarSkin(avatarId=" + this.a + ", avatarProfession=" + this.b + ", elementType=" + this.c + ", res=" + this.d + ", itemName=" + this.e + ", coverColorValue=" + this.f + ", skinId=" + this.g + ")";
        }

        public a(@l String str, @l String str2, @l String str3, @l String str4, @l String str5, @l String str6, @l String str7) {
            K.p(str, "avatarId");
            K.p(str2, "avatarProfession");
            K.p(str3, "elementType");
            K.p(str4, "res");
            K.p(str5, "itemName");
            K.p(str6, "coverColorValue");
            K.p(str7, "skinId");
            this.a = str;
            this.b = str2;
            this.c = str3;
            this.d = str4;
            this.e = str5;
            this.f = str6;
            this.g = str7;
        }

        public /* synthetic */ a(String str, String str2, String str3, String str4, String str5, String str6, String str7, int i2, DefaultConstructorMarker defaultConstructorMarker) {
            this((i2 & 1) != 0 ? "" : str, (i2 & 2) != 0 ? "" : str2, (i2 & 4) != 0 ? "" : str3, (i2 & 8) != 0 ? "" : str4, (i2 & 16) != 0 ? "" : str5, (i2 & 32) != 0 ? "" : str6, (i2 & 64) != 0 ? "" : str7);
        }
    }

    @v(parameters = 0)
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class b {
        public static final int e = 8;

        @SerializedName("icon_url")
        @l
        public final String a;

        @SerializedName("select_icon_url")
        @l
        public final String b;

        @SerializedName("id")
        @l
        public final String c;
        public boolean d;

        public b() {
            this(null, null, null, 7, null);
        }

        public static /* synthetic */ b e(b bVar, String str, String str2, String str3, int i, Object obj) {
            if ((i & 1) != 0) {
                str = bVar.a;
            }
            if ((i & 2) != 0) {
                str2 = bVar.b;
            }
            if ((i & 4) != 0) {
                str3 = bVar.c;
            }
            return bVar.d(str, str2, str3);
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
        public final b d(@l String str, @l String str2, @l String str3) {
            K.p(str, "iconUrl");
            K.p(str2, "selectedIconUrl");
            K.p(str3, "id");
            return new b(str, str2, str3);
        }

        public boolean equals(@m Object obj) {
            if (this == obj) {
                return true;
            }
            if (!(obj instanceof b)) {
                return false;
            }
            b bVar = (b) obj;
            return K.g(this.a, bVar.a) && K.g(this.b, bVar.b) && K.g(this.c, bVar.c);
        }

        @l
        public final String f() {
            return this.a;
        }

        @l
        public final String g() {
            return this.c;
        }

        @l
        public final String h() {
            return this.b;
        }

        public int hashCode() {
            return (((this.a.hashCode() * 31) + this.b.hashCode()) * 31) + this.c.hashCode();
        }

        public final boolean i() {
            return this.d;
        }

        public final void j(boolean z) {
            this.d = z;
        }

        @l
        public String toString() {
            return "Type(iconUrl=" + this.a + ", selectedIconUrl=" + this.b + ", id=" + this.c + ")";
        }

        public b(@l String str, @l String str2, @l String str3) {
            K.p(str, "iconUrl");
            K.p(str2, "selectedIconUrl");
            K.p(str3, "id");
            this.a = str;
            this.b = str2;
            this.c = str3;
        }

        public /* synthetic */ b(String str, String str2, String str3, int i, DefaultConstructorMarker defaultConstructorMarker) {
            this((i & 1) != 0 ? "" : str, (i & 2) != 0 ? "" : str2, (i & 4) != 0 ? "" : str3);
        }
    }

    public AvatarBackground() {
        this(null, null, null, 7, null);
    }

    /* JADX WARN: Multi-variable type inference failed */
    public static /* synthetic */ AvatarBackground copy$default(AvatarBackground avatarBackground, List list, List list2, List list3, int i, Object obj) {
        if ((i & 1) != 0) {
            list = avatarBackground.avatarProfessions;
        }
        if ((i & 2) != 0) {
            list2 = avatarBackground.avatarSkins;
        }
        if ((i & 4) != 0) {
            list3 = avatarBackground.elementTypes;
        }
        return avatarBackground.copy(list, list2, list3);
    }

    @l
    public final List<b> component1() {
        return this.avatarProfessions;
    }

    @l
    public final List<a> component2() {
        return this.avatarSkins;
    }

    @l
    public final List<b> component3() {
        return this.elementTypes;
    }

    @l
    public final AvatarBackground copy(@l List<b> list, @l List<a> list2, @l List<b> list3) {
        K.p(list, "avatarProfessions");
        K.p(list2, "avatarSkins");
        K.p(list3, "elementTypes");
        return new AvatarBackground(list, list2, list3);
    }

    public boolean equals(@m Object obj) {
        if (this == obj) {
            return true;
        }
        if (!(obj instanceof AvatarBackground)) {
            return false;
        }
        AvatarBackground avatarBackground = (AvatarBackground) obj;
        return K.g(this.avatarProfessions, avatarBackground.avatarProfessions) && K.g(this.avatarSkins, avatarBackground.avatarSkins) && K.g(this.elementTypes, avatarBackground.elementTypes);
    }

    @l
    public final List<b> getAvatarProfessions() {
        return this.avatarProfessions;
    }

    @l
    public final List<a> getAvatarSkins() {
        return this.avatarSkins;
    }

    @l
    public final List<b> getElementTypes() {
        return this.elementTypes;
    }

    public int hashCode() {
        return (((this.avatarProfessions.hashCode() * 31) + this.avatarSkins.hashCode()) * 31) + this.elementTypes.hashCode();
    }

    @l
    public String toString() {
        return "AvatarBackground(avatarProfessions=" + this.avatarProfessions + ", avatarSkins=" + this.avatarSkins + ", elementTypes=" + this.elementTypes + ")";
    }

    public AvatarBackground(@l List<b> list, @l List<a> list2, @l List<b> list3) {
        K.p(list, "avatarProfessions");
        K.p(list2, "avatarSkins");
        K.p(list3, "elementTypes");
        this.avatarProfessions = list;
        this.avatarSkins = list2;
        this.elementTypes = list3;
    }

    public /* synthetic */ AvatarBackground(List list, List list2, List list3, int i, DefaultConstructorMarker defaultConstructorMarker) {
        this((i & 1) != 0 ? H.H() : list, (i & 2) != 0 ? H.H() : list2, (i & 4) != 0 ? H.H() : list3);
    }
}
