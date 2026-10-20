package com.mihoyo.hyperion.biz.miniwidget.napavatar.settings;

import Ce.P;
import Ce.P0;
import G50.l;
import G50.m;
import We.B;
import Xe.E;
import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.view.InflateException;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ImageView;
import android.widget.LinearLayout;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.widget.AppCompatImageView;
import androidx.constraintlayout.widget.ConstraintLayout;
import androidx.lifecycle.Z;
import androidx.lifecycle.z0;
import androidx.recyclerview.widget.GridLayoutManager;
import androidx.recyclerview.widget.RecyclerView;
import com.bumptech.glide.n;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.NapAvatarWidgetProvider;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.NapSettingsActivity;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.view.NapSettingTabView;
import com.mihoyo.hyperion.utils.TopActivityHelper;
import hg.m;
import java.util.ArrayList;
import java.util.List;
import jg.r;
import kotlin.Lazy;
import kotlin.jvm.functions.Function0;
import kotlin.jvm.functions.Function1;
import kotlin.jvm.internal.C;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import qg.k;
import rg.C0792a;
import x0.v;
import zy.G;
import zy.J;
import zy.N0;
import zy.V;
import zy.x;

@v(parameters = 0)
@r0({"SMAP\nNapSettingsActivity.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapSettingsActivity.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/settings/NapSettingsActivity\n+ 2 ViewBindingHelper.kt\ncom/mihoyo/hyperion/utils/ViewBindingHelperKt\n+ 3 View.kt\nandroidx/core/view/ViewKt\n+ 4 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,172:1\n12#2:173\n256#3,2:174\n256#3,2:176\n256#3,2:181\n256#3,2:183\n256#3,2:185\n256#3,2:187\n256#3,2:189\n774#4:178\n865#4,2:179\n*S KotlinDebug\n*F\n+ 1 NapSettingsActivity.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/settings/NapSettingsActivity\n*L\n39#1:173\n91#1:174,2\n92#1:176,2\n169#1:181,2\n104#1:183,2\n108#1:185,2\n116#1:187,2\n117#1:189,2\n153#1:178\n153#1:179,2\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapSettingsActivity extends AppCompatActivity {

    @l
    public static final a g = new a(null);
    public static final int h = 8;

    @m
    public AvatarBackground.b c;

    @m
    public AvatarBackground.b d;

    @l
    public final Lazy a = G.b(new e(this));

    @l
    public final List<AvatarBackground.a> b = new ArrayList();

    @l
    public final Lazy e = G.b(new Function0() { // from class: qg.g
        public final Object invoke() {
            k C4;
            C4 = NapSettingsActivity.C4(NapSettingsActivity.this);
            return C4;
        }
    });

    @l
    public final Lazy f = G.b(new Function0() { // from class: qg.h
        public final Object invoke() {
            com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a R4;
            R4 = NapSettingsActivity.R4(NapSettingsActivity.this);
            return R4;
        }
    });

    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a {
        public /* synthetic */ a(DefaultConstructorMarker defaultConstructorMarker) {
            this();
        }

        public final void a(@l Context context) {
            K.p(context, "context");
            Intent intent = new Intent(context, (Class<?>) NapSettingsActivity.class);
            Activity topActivity = TopActivityHelper.INSTANCE.getTopActivity();
            if (topActivity != null) {
                topActivity.startActivity(intent);
            } else {
                intent.addFlags(268435456);
                context.startActivity(intent);
            }
        }

        public a() {
        }
    }

    @v(parameters = 1)
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class b extends hj.a {
        public static final int j = 0;
        public final int i;

        public b() {
            this(0, 1, null);
        }

        public int o() {
            return this.i;
        }

        public b(int i) {
            this.i = i;
        }

        public /* synthetic */ b(int i, int i2, DefaultConstructorMarker defaultConstructorMarker) {
            this((i2 & 1) != 0 ? P.Z(10) : i);
        }
    }

    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public /* synthetic */ class c {
        public static final /* synthetic */ int[] a;

        static {
            int[] iArr = new int[a.EnumC0042a.values().length];
            try {
                iArr[a.EnumC0042a.SUCCESS.ordinal()] = 1;
            } catch (NoSuchFieldError unused) {
            }
            try {
                iArr[a.EnumC0042a.ERROR.ordinal()] = 2;
            } catch (NoSuchFieldError unused2) {
            }
            try {
                iArr[a.EnumC0042a.LOADING.ordinal()] = 3;
            } catch (NoSuchFieldError unused3) {
            }
            a = iArr;
        }
    }

    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class d implements Z, C {
        public final /* synthetic */ Function1 a;

        public d(Function1 function1) {
            K.p(function1, "function");
            this.a = function1;
        }

        public final boolean equals(@m Object obj) {
            if ((obj instanceof Z) && (obj instanceof C)) {
                return K.g(getFunctionDelegate(), ((C) obj).getFunctionDelegate());
            }
            return false;
        }

        @l
        public final x<?> getFunctionDelegate() {
            return this.a;
        }

        public final int hashCode() {
            return getFunctionDelegate().hashCode();
        }

        public final /* synthetic */ void onChanged(Object obj) {
            this.a.invoke(obj);
        }
    }

    @r0({"SMAP\nViewBindingHelper.kt\nKotlin\n*S Kotlin\n*F\n+ 1 ViewBindingHelper.kt\ncom/mihoyo/hyperion/utils/ViewBindingHelperKt$lazyBind$1\n+ 2 ViewBindingHelper.kt\ncom/mihoyo/hyperion/utils/ViewBindingHelperKt\n*L\n1#1,115:1\n23#2:116\n59#2,11:117\n86#2:128\n*S KotlinDebug\n*F\n+ 1 ViewBindingHelper.kt\ncom/mihoyo/hyperion/utils/ViewBindingHelperKt$lazyBind$1\n*L\n12#1:116\n12#1:117,11\n12#1:128\n*E\n"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class e implements Function0<r> {
        public final /* synthetic */ Activity a;

        public e(Activity activity) {
            this.a = activity;
        }

        /* JADX WARN: Type inference failed for: r0v5, types: [jg.r, q5.b] */
        /* renamed from: a, reason: merged with bridge method [inline-methods] */
        public final r invoke() {
            LayoutInflater layoutInflater = this.a.getLayoutInflater();
            K.o(layoutInflater, "getLayoutInflater(...)");
            Object invoke = r.class.getMethod("inflate", LayoutInflater.class).invoke(null, layoutInflater);
            if (invoke instanceof r) {
                return (q5.b) invoke;
            }
            throw new InflateException("Cant inflate ViewBinding " + r.class.getName());
        }
    }

    public static final k C4(final NapSettingsActivity napSettingsActivity) {
        return new k(new ArrayList(), new Function1() { // from class: qg.i
            public final Object invoke(Object obj) {
                N0 D4;
                D4 = NapSettingsActivity.D4(NapSettingsActivity.this, (AvatarBackground.a) obj);
                return D4;
            }
        });
    }

    /* JADX WARN: Multi-variable type inference failed */
    public static final N0 D4(NapSettingsActivity napSettingsActivity, AvatarBackground.a aVar) {
        K.p(aVar, "selectData");
        NapAvatarWidgetProvider.b.a("WallPaper", false, aVar.d());
        C0792a.a.d(aVar);
        napSettingsActivity.finish();
        return N0.a;
    }

    public static final N0 I4(NapSettingsActivity napSettingsActivity, a.EnumC0042a enumC0042a) {
        com.bumptech.glide.m C;
        com.bumptech.glide.m G0;
        napSettingsActivity.P4();
        int i = enumC0042a == null ? -1 : c.a[enumC0042a.ordinal()];
        if (i != 1) {
            if (i == 2) {
                LinearLayout linearLayout = napSettingsActivity.G4().f;
                K.o(linearLayout, "errorView");
                linearLayout.setVisibility(0);
            } else {
                if (i != 3) {
                    throw new J();
                }
                ImageView imageView = napSettingsActivity.G4().g;
                K.o(imageView, "loadingView");
                imageView.setVisibility(0);
                n n = E.a.n(napSettingsActivity.G4().g);
                if (n != null && (C = n.C(Integer.valueOf(m.h.mn))) != null && (G0 = C.G0(true)) != null) {
                    G0.l1(napSettingsActivity.G4().g);
                }
            }
        }
        NapSettingTabView napSettingTabView = napSettingsActivity.G4().d;
        K.o(napSettingTabView, "elementView");
        a.EnumC0042a enumC0042a2 = a.EnumC0042a.SUCCESS;
        napSettingTabView.setVisibility(enumC0042a == enumC0042a2 ? 0 : 8);
        NapSettingTabView napSettingTabView2 = napSettingsActivity.G4().i;
        K.o(napSettingTabView2, "professionView");
        napSettingTabView2.setVisibility(enumC0042a == enumC0042a2 ? 0 : 8);
        return N0.a;
    }

    public static final N0 J4(NapSettingsActivity napSettingsActivity, List list) {
        napSettingsActivity.b.clear();
        List<AvatarBackground.a> list2 = napSettingsActivity.b;
        K.m(list);
        list2.addAll(list);
        napSettingsActivity.F4().A().clear();
        napSettingsActivity.F4().A().addAll(napSettingsActivity.b);
        napSettingsActivity.F4().notifyDataSetChanged();
        napSettingsActivity.E4();
        return N0.a;
    }

    public static final N0 K4(final NapSettingsActivity napSettingsActivity, V v) {
        List<AvatarBackground.b> list = (List) v.e();
        List<AvatarBackground.b> list2 = (List) v.f();
        napSettingsActivity.G4().d.d("属性", list, new Function1() { // from class: qg.e
            public final Object invoke(Object obj) {
                N0 L4;
                L4 = NapSettingsActivity.L4(NapSettingsActivity.this, (AvatarBackground.b) obj);
                return L4;
            }
        });
        napSettingsActivity.G4().i.d("特性", list2, new Function1() { // from class: qg.f
            public final Object invoke(Object obj) {
                N0 M4;
                M4 = NapSettingsActivity.M4(NapSettingsActivity.this, (AvatarBackground.b) obj);
                return M4;
            }
        });
        return N0.a;
    }

    public static final N0 L4(NapSettingsActivity napSettingsActivity, AvatarBackground.b bVar) {
        napSettingsActivity.d = bVar;
        napSettingsActivity.Q4();
        return N0.a;
    }

    public static final N0 M4(NapSettingsActivity napSettingsActivity, AvatarBackground.b bVar) {
        napSettingsActivity.c = bVar;
        napSettingsActivity.Q4();
        return N0.a;
    }

    /* JADX WARN: Multi-variable type inference failed */
    private final void N4() {
        RecyclerView recyclerView = G4().c;
        recyclerView.setAdapter(F4());
        recyclerView.setLayoutManager(new GridLayoutManager(this, 3));
        recyclerView.addItemDecoration(new b(0, 1, null));
        AppCompatImageView appCompatImageView = G4().b;
        K.o(appCompatImageView, "backView");
        P.o0(appCompatImageView, new Function0() { // from class: qg.a
            public final Object invoke() {
                N0 O4;
                O4 = NapSettingsActivity.O4(NapSettingsActivity.this);
                return O4;
            }
        });
    }

    /* JADX WARN: Multi-variable type inference failed */
    public static final N0 O4(NapSettingsActivity napSettingsActivity) {
        napSettingsActivity.finish();
        return N0.a;
    }

    private final void P4() {
        ImageView imageView = G4().g;
        K.o(imageView, "loadingView");
        imageView.setVisibility(8);
        LinearLayout linearLayout = G4().f;
        K.o(linearLayout, "errorView");
        linearLayout.setVisibility(8);
    }

    public static final com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a R4(NapSettingsActivity napSettingsActivity) {
        return (com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a) new z0(napSettingsActivity).c(com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a.class);
    }

    private final void initData() {
        H4().r().k(this, new d(new Function1() { // from class: qg.b
            public final Object invoke(Object obj) {
                N0 I4;
                I4 = NapSettingsActivity.I4(NapSettingsActivity.this, (a.EnumC0042a) obj);
                return I4;
            }
        }));
        H4().o().k(this, new d(new Function1() { // from class: qg.c
            public final Object invoke(Object obj) {
                N0 J4;
                J4 = NapSettingsActivity.J4(NapSettingsActivity.this, (List) obj);
                return J4;
            }
        }));
        H4().s().k(this, new d(new Function1() { // from class: qg.d
            public final Object invoke(Object obj) {
                N0 K4;
                K4 = NapSettingsActivity.K4(NapSettingsActivity.this, (V) obj);
                return K4;
            }
        }));
        H4().p();
    }

    public final void E4() {
        LinearLayout linearLayout = G4().e;
        K.o(linearLayout, "emptyView");
        linearLayout.setVisibility(F4().A().isEmpty() ? 0 : 8);
    }

    public final k F4() {
        return (k) this.e.getValue();
    }

    public final r G4() {
        return (r) this.a.getValue();
    }

    public final com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a H4() {
        return (com.mihoyo.hyperion.biz.miniwidget.napavatar.settings.a) this.f.getValue();
    }

    public final void Q4() {
        String p;
        String o;
        if (this.c == null && this.d == null) {
            F4().A().clear();
            F4().A().addAll(this.b);
            F4().notifyDataSetChanged();
        } else {
            List<AvatarBackground.a> list = this.b;
            ArrayList arrayList = new ArrayList();
            for (Object obj : list) {
                AvatarBackground.a aVar = (AvatarBackground.a) obj;
                AvatarBackground.b bVar = this.d;
                if (bVar == null || (p = bVar.g()) == null) {
                    p = aVar.p();
                }
                AvatarBackground.b bVar2 = this.c;
                if (bVar2 == null || (o = bVar2.g()) == null) {
                    o = aVar.o();
                }
                if (K.g(aVar.p(), p) && K.g(aVar.o(), o)) {
                    arrayList.add(obj);
                }
            }
            F4().A().clear();
            F4().A().addAll(arrayList);
            F4().notifyDataSetChanged();
        }
        E4();
        G4().c.scrollToPosition(0);
    }

    /* JADX WARN: Multi-variable type inference failed */
    public void onCreate(@G50.m Bundle bundle) {
        super/*androidx.fragment.app.FragmentActivity*/.onCreate(bundle);
        setContentView(G4().getRoot());
        B.b.l(B.g, this, false, 2, (Object) null);
        ConstraintLayout root = G4().getRoot();
        K.o(root, "getRoot(...)");
        B.c.a aVar = B.c.e;
        We.E.h(root, aVar.b(), (View) null, 2, (Object) null);
        ConstraintLayout constraintLayout = G4().h;
        K.o(constraintLayout, "navigationBar");
        We.E.h(constraintLayout, aVar.c(), (View) null, 2, (Object) null);
        P0 p0 = P0.a;
        p0.Z(this, 2105376);
        p0.L(this);
        N4();
        initData();
    }
}
