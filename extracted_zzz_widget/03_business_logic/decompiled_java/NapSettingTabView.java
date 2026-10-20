package com.mihoyo.hyperion.biz.miniwidget.napavatar.view;

import Ce.P;
import G50.l;
import Ok.i;
import Xe.E;
import Zy.j;
import android.content.Context;
import android.util.AttributeSet;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import androidx.recyclerview.widget.RecyclerView;
import com.bumptech.glide.m;
import com.bumptech.glide.n;
import com.google.android.flexbox.FlexboxLayoutManager;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground;
import com.mihoyo.hyperion.biz.miniwidget.napavatar.view.NapSettingTabView;
import java.util.ArrayList;
import java.util.List;
import jg.C0633u;
import kotlin.jvm.functions.Function0;
import kotlin.jvm.functions.Function1;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import x0.v;
import zy.N0;

@v(parameters = 0)
@r0({"SMAP\nNapSettingTabView.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapSettingTabView.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/view/NapSettingTabView\n+ 2 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,94:1\n1869#2,2:95\n*S KotlinDebug\n*F\n+ 1 NapSettingTabView.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/view/NapSettingTabView\n*L\n49#1:95,2\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class NapSettingTabView extends LinearLayout {
    public static final int d = 8;

    @l
    public final C0633u a;

    @l
    public final a b;

    @l
    public Function1<? super AvatarBackground.b, N0> c;

    @r0({"SMAP\nNapSettingTabView.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapSettingTabView.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/view/NapSettingTabView$SettingAdapter\n+ 2 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,94:1\n1869#2,2:95\n*S KotlinDebug\n*F\n+ 1 NapSettingTabView.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/view/NapSettingTabView$SettingAdapter\n*L\n73#1:95,2\n*E\n"})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public final class a extends RecyclerView.h<C0043a> {

        @l
        public final List<AvatarBackground.b> e;
        public final /* synthetic */ NapSettingTabView f;

        /* renamed from: com.mihoyo.hyperion.biz.miniwidget.napavatar.view.NapSettingTabView$a$a, reason: collision with other inner class name */
        /* loaded from: X:\AI project\解包\tools\classes12.dex */
        public final class C0043a extends RecyclerView.F {

            @l
            public final NapSettingTabItemView a;
            public final /* synthetic */ a b;

            /* JADX WARN: 'super' call moved to the top of the method (can break code semantics) */
            public C0043a(@l a aVar, NapSettingTabItemView napSettingTabItemView) {
                super(napSettingTabItemView);
                K.p(napSettingTabItemView, "view");
                this.b = aVar;
                this.a = napSettingTabItemView;
            }

            public final void p(@l AvatarBackground.b bVar) {
                K.p(bVar, "type");
                this.a.a(bVar);
            }

            @l
            public final NapSettingTabItemView r() {
                return this.a;
            }
        }

        public a(@l NapSettingTabView napSettingTabView, List<AvatarBackground.b> list) {
            K.p(list, "data");
            this.f = napSettingTabView;
            this.e = list;
        }

        public static final N0 C(AvatarBackground.b bVar, a aVar, NapSettingTabView napSettingTabView) {
            bVar.j(!bVar.i());
            if (bVar.i()) {
                for (AvatarBackground.b bVar2 : aVar.e) {
                    if (!K.g(bVar2, bVar)) {
                        bVar2.j(false);
                    }
                }
                napSettingTabView.c.invoke(bVar);
            } else {
                napSettingTabView.c.invoke((Object) null);
            }
            napSettingTabView.b.notifyDataSetChanged();
            return N0.a;
        }

        @l
        public final List<AvatarBackground.b> A() {
            return this.e;
        }

        /* renamed from: B, reason: merged with bridge method [inline-methods] */
        public void onBindViewHolder(@l C0043a c0043a, int i) {
            K.p(c0043a, "holder");
            if (i < 0 || i >= this.e.size()) {
                return;
            }
            final AvatarBackground.b bVar = this.e.get(i);
            c0043a.p(bVar);
            View view = ((RecyclerView.F) c0043a).itemView;
            K.o(view, "itemView");
            final NapSettingTabView napSettingTabView = this.f;
            P.o0(view, new Function0() { // from class: sg.b
                public final Object invoke() {
                    N0 C;
                    C = NapSettingTabView.a.C(AvatarBackground.b.this, this, napSettingTabView);
                    return C;
                }
            });
        }

        @l
        /* renamed from: D, reason: merged with bridge method [inline-methods] */
        public C0043a onCreateViewHolder(@l ViewGroup viewGroup, int i) {
            K.p(viewGroup, "parent");
            Context context = viewGroup.getContext();
            K.o(context, "getContext(...)");
            return new C0043a(this, new NapSettingTabItemView(context, null, 0, 6, null));
        }

        public int getItemCount() {
            return this.e.size();
        }
    }

    /* JADX WARN: 'this' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingTabView(@l Context context) {
        this(context, null, 0, 6, null);
        K.p(context, "context");
    }

    public static final N0 e(AvatarBackground.b bVar) {
        return N0.a;
    }

    public final void d(@l String str, @l List<AvatarBackground.b> list, @l Function1<? super AvatarBackground.b, N0> function1) {
        m E;
        K.p(str, "name");
        K.p(list, "types");
        K.p(function1, "selectListener");
        this.a.b.setText(str);
        this.b.A().clear();
        this.b.A().addAll(list);
        this.b.notifyDataSetChanged();
        for (AvatarBackground.b bVar : list) {
            n m = E.a.m(this.a.getRoot().getContext());
            if (m != null && (E = m.E(bVar.h())) != null) {
                E.C1();
            }
        }
        this.c = function1;
    }

    /* JADX WARN: 'this' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingTabView(@l Context context, @G50.m AttributeSet attributeSet) {
        this(context, attributeSet, 0, 4, null);
        K.p(context, "context");
    }

    public /* synthetic */ NapSettingTabView(Context context, AttributeSet attributeSet, int i, int i2, DefaultConstructorMarker defaultConstructorMarker) {
        this(context, (i2 & 2) != 0 ? null : attributeSet, (i2 & 4) != 0 ? 0 : i);
    }

    /* JADX WARN: 'super' call moved to the top of the method (can break code semantics) */
    @j
    public NapSettingTabView(@l Context context, @G50.m AttributeSet attributeSet, int i) {
        super(context, attributeSet, i);
        K.p(context, "context");
        C0633u a2 = C0633u.a(LayoutInflater.from(context), this);
        K.o(a2, "inflate(...)");
        this.a = a2;
        a aVar = new a(this, new ArrayList());
        this.b = aVar;
        this.c = new Function1() { // from class: sg.a
            public final Object invoke(Object obj) {
                N0 e;
                e = NapSettingTabView.e((AvatarBackground.b) obj);
                return e;
            }
        };
        RecyclerView recyclerView = a2.c;
        recyclerView.setLayoutManager(new FlexboxLayoutManager(context, 0));
        recyclerView.addItemDecoration(new i(P.Z(8), P.Z(8), P.Z(8), P.Z(8)));
        recyclerView.setAdapter(aVar);
    }
}
